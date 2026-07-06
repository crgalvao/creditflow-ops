using CreditFlow.Api.Extensions;
using CreditFlow.Domain.SharedKernel;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CreditFlow.Api.Errors;

public sealed partial class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            DomainException domainException => new ProblemDetails
            {
                Title = "Business rule violation",
                Detail = domainException.Message,
                Status = StatusCodes.Status400BadRequest
            },

            InvalidWorkflowStateException workflowException => new ProblemDetails
            {
                Title = "Invalid workflow state",
                Detail = workflowException.Message,
                Status = StatusCodes.Status409Conflict
            },

            _ => new ProblemDetails
            {
                Title = "Unexpected API error",
                Detail = "An unexpected error occurred while processing the request.",
                Status = StatusCodes.Status500InternalServerError
            }
        };

        if (problem.Status == StatusCodes.Status500InternalServerError)
        {
            logger.LogUnexpectedSystemError(exception);
        }
        else
        {
            logger.LogBusinessRuleViolation(exception, exception.Message);
        }

        problem.Extensions["correlationId"] = httpContext.GetOrCreateCorrelationId();

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);

        return true;
    }
}
