using CreditFlow.Api.Extensions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CreditFlow.Api.Errors;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var statusCode = exception.ToHttpStatusCode();
        var correlationId = httpContext.GetOrCreateCorrelationId();

        if (statusCode.IsServerError())
        {
            logger.LogUnexpectedSystemErrorWithCorrelationId(exception, correlationId);
        }
        else
        {
            logger.LogBusinessRuleViolationWithCorrelationId(
                exception,
                exception.Message,
                correlationId);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = exception.ToProblemTitle(),
            Detail = exception.ToProblemDetail(environment, statusCode),
            Instance = httpContext.Request.Path
        };

        problemDetails.AddCorrelationMetadata(
            httpContext,
            correlationId,
            environment,
            exception);

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        var written = await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });

        if (!written)
        {
            await httpContext.Response.WriteAsJsonAsync(
                problemDetails,
                cancellationToken);
        }

        return true;
    }
}
