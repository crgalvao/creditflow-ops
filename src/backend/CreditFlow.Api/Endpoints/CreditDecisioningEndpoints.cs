using CreditFlow.Api.Contracts.CreditDecisioning;
using CreditFlow.Api.Errors;
using CreditFlow.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace CreditFlow.Api.Endpoints;

public static class CreditDecisioningEndpoints
{
    public static IEndpointRouteBuilder MapCreditDecisioningEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/internal/credit")
            .WithTags("Internal - Credit Decisioning");

        group.MapPost("/applications/{applicationId}/assess", CompleteCreditAssessmentAsync)
            .WithName("CompleteCreditAssessment")
            .WithSummary("Simulates the async credit assessment worker.")
            .WithDescription("This endpoint is internal/local-demo oriented. In AWS, an SQS-triggered worker should process CreditAssessmentRequested and publish CreditAssessmentCompleted.")
            .Produces<CreditAssessmentResultResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/applications/{applicationId}/profiles/upsert", UpsertCreditProfileAndCompleteDecisionAsync)
            .WithName("UpsertCreditProfileAndCompleteDecision")
            .WithSummary("Upserts the credit profile from the latest assessment and completes the loan decision.")
            .WithDescription("This endpoint is internal/local-demo oriented. In AWS, this behavior belongs in a worker reacting to CreditAssessmentCompleted.")
            .Produces<CreditProfileUpsertResultResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Results<Accepted<CreditAssessmentResultResponse>, ValidationProblem, NotFound<ProblemDetails>>> CompleteCreditAssessmentAsync(
        string applicationId,
        CompleteCreditAssessmentRequest request,
        ApplicationWorkflowService workflow,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return TypedResults.ValidationProblem(validationErrors);
        }

        var result = await workflow.CompleteCreditAssessmentAsync(
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

    private static async Task<Results<Accepted<CreditProfileUpsertResultResponse>, ValidationProblem, NotFound<ProblemDetails>>> UpsertCreditProfileAndCompleteDecisionAsync(
        string applicationId,
        UpsertCreditProfileRequest request,
        ApplicationWorkflowService workflow,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return TypedResults.ValidationProblem(validationErrors);
        }

        var result = await workflow.UpsertCreditProfileAndCompleteDecisionAsync(
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
            $"/applications/{applicationId}/decision",
            result);
    }
}
