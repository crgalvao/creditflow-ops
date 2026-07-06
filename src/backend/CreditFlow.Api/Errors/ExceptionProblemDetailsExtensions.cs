using CreditFlow.Domain.SharedKernel;
using Microsoft.AspNetCore.Mvc;

namespace CreditFlow.Api.Errors;

public static class ExceptionProblemDetailsExtensions
{
    public static int ToHttpStatusCode(this Exception exception)
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

    public static string ToProblemTitle(this Exception exception)
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

    public static string ToProblemDetail(
        this Exception exception,
        IHostEnvironment environment,
        int statusCode)
    {
        if (environment.IsDevelopment())
        {
            return exception.Message;
        }

        return statusCode.IsServerError()
            ? "An unexpected error occurred while processing the request."
            : exception.Message;
    }

    public static bool IsServerError(this int statusCode)
    {
        return statusCode >= StatusCodes.Status500InternalServerError;
    }

    public static void AddCorrelationMetadata(
        this ProblemDetails problemDetails,
        HttpContext httpContext,
        string correlationId,
        IHostEnvironment environment,
        Exception exception)
    {
        problemDetails.Extensions["correlationId"] = correlationId;
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        if (environment.IsDevelopment())
        {
            problemDetails.Extensions["exceptionType"] = exception.GetType().FullName;
        }
    }
}
