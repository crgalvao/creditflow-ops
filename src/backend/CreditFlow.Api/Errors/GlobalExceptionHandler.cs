using CreditFlow.Api.Extensions;
using CreditFlow.Domain.SharedKernel;
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
        var statusCode = GetStatusCode(exception);
        var correlationId = httpContext.GetOrCreateCorrelationId();

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogUnexpectedSystemErrorWithCorrelationId(exception, correlationId);
        }
        else
        {
            logger.LogBusinessRuleViolationWithCorrelationId(exception, exception.Message, correlationId);
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = GetTitle(exception),
            Detail = GetDetail(exception, statusCode),
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["correlationId"] = correlationId;
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        if (environment.IsDevelopment())
        {
            problemDetails.Extensions["exceptionType"] = exception.GetType().FullName;
        }

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

    private static int GetStatusCode(Exception exception)
    {
        return exception switch
        {
            InvalidWorkflowStateException => StatusCodes.Status409Conflict,
            DomainException => StatusCodes.Status400BadRequest,
            ArgumentException => StatusCodes.Status400BadRequest,
            BadHttpRequestException => StatusCodes.Status400BadRequest,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status500InternalServerError
        };
    }

    private static string GetTitle(Exception exception)
    {
        return exception switch
        {
            InvalidWorkflowStateException => "Invalid workflow state",
            DomainException => "Domain rule violation",
            ArgumentException => "Invalid request",
            BadHttpRequestException => "Invalid request",
            KeyNotFoundException => "Not found",
            _ => "Unexpected error"
        };
    }

    private string GetDetail(Exception exception, int statusCode)
    {
        if (environment.IsDevelopment())
        {
            return exception.Message;
        }

        return statusCode >= StatusCodes.Status500InternalServerError
            ? "An unexpected error occurred while processing the request."
            : exception.Message;
    }
}
