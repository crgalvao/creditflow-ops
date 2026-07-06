using CreditFlow.Api.Contracts.Kyc;
using CreditFlow.Api.Errors;
using CreditFlow.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace CreditFlow.Api.Endpoints;

public static class KycEndpoints
{
    public static IEndpointRouteBuilder MapKycEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/internal/kyc")
            .WithTags("Internal - KYC");

        group.MapPost("/applications/{applicationId}/complete", CompleteKycAsync)
            .WithName("CompleteKycCheck")
            .WithSummary("Simulates the async KYC worker result for an application.")
            .WithDescription("This endpoint is internal/local-demo oriented. In AWS, an SQS-triggered worker should process KycCheckRequested and publish KycCompleted, KycFailed, or KycNeedsReview.")
            .Produces<KycResultResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Results<Accepted<KycResultResponse>, ValidationProblem, NotFound<ProblemDetails>>> CompleteKycAsync(
        string applicationId,
        CompleteKycRequest request,
        ApplicationWorkflowService workflow,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return TypedResults.ValidationProblem(validationErrors);
        }

        var result = await workflow.CompleteKycAsync(
            applicationId,
            request,
            httpContext.GetOrCreateCorrelationId(),
            cancellationToken);

        if (result is null)
        {
            return TypedResults.NotFound(new ProblemDetails
            {
                Title = "Not found",
                Detail = $"Loan application '{applicationId}' was not found.",
                Status = StatusCodes.Status404NotFound
            });
        }

        return TypedResults.Accepted(
            $"/applications/{applicationId}",
            result);
    }
}
