using CreditFlow.Api.Contracts.Applications;
using CreditFlow.Api.Errors;
using CreditFlow.Api.Mapping;
using CreditFlow.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace CreditFlow.Api.Endpoints;

public static class ApplicationEndpoints
{
    public static IEndpointRouteBuilder MapApplicationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/applications")
            .WithTags("Applications");

        group.MapPost("/", SubmitApplicationAsync)
            .WithName("SubmitLoanApplication")
            .WithSummary("Submits a new loan application and starts the event-driven decision flow.")
            .Produces<SubmitLoanApplicationResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem();

        group.MapGet("/", ListApplicationsAsync)
            .WithName("ListLoanApplications")
            .WithSummary("Lists loan applications in memory.")
            .Produces<IReadOnlyCollection<LoanApplicationSummaryResponse>>();

        group.MapGet("/{applicationId}", GetApplicationAsync)
            .WithName("GetLoanApplication")
            .WithSummary("Gets loan application details.")
            .Produces<LoanApplicationDetailsResponse>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{applicationId}/timeline", GetTimelineAsync)
            .WithName("GetLoanApplicationTimeline")
            .WithSummary("Gets the event timeline for a loan application.")
            .Produces<IReadOnlyCollection<DomainEventResponse>>()
            .Produces(StatusCodes.Status404NotFound);

        group.MapGet("/{applicationId}/decision", GetDecisionAsync)
            .WithName("GetLoanApplicationDecision")
            .WithSummary("Gets the current or final decision details for a loan application.")
            .Produces<LoanDecisionResponse>()
            .Produces(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<Results<Accepted<SubmitLoanApplicationResponse>, ValidationProblem>> SubmitApplicationAsync(
        CreateLoanApplicationRequest request,
        ApplicationWorkflowService workflow,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var validationErrors = request.Validate();

        if (validationErrors.Count > 0)
        {
            return TypedResults.ValidationProblem(validationErrors);
        }

        var result = await workflow.SubmitApplicationAsync(
            request,
            httpContext.GetOrCreateCorrelationId(),
            cancellationToken);

        return TypedResults.Accepted(
            $"/applications/{result.ApplicationId}",
            result);
    }

    private static async Task<Ok<IReadOnlyCollection<LoanApplicationSummaryResponse>>> ListApplicationsAsync(
        ApplicationWorkflowService workflow,
        CancellationToken cancellationToken)
    {
        var applications = await workflow.ListApplicationsAsync(cancellationToken);

        return TypedResults.Ok<IReadOnlyCollection<LoanApplicationSummaryResponse>>(
            [.. applications.Select(ApplicationMappings.ToSummaryResponse)]
        );
    }

    private static async Task<Results<Ok<LoanApplicationDetailsResponse>, NotFound<ProblemDetails>>> GetApplicationAsync(
        string applicationId,
        ApplicationWorkflowService workflow,
        CancellationToken cancellationToken)
    {
        var application = await workflow.GetApplicationAsync(applicationId, cancellationToken);

        if (application is null)
        {
            return NotFoundProblem($"Loan application '{applicationId}' was not found.");
        }

        return TypedResults.Ok(application.ToDetailsResponse());
    }

    private static async Task<Results<Ok<IReadOnlyCollection<DomainEventResponse>>, NotFound<ProblemDetails>>> GetTimelineAsync(
        string applicationId,
        ApplicationWorkflowService workflow,
        CancellationToken cancellationToken)
    {
        var exists = await workflow.ApplicationExistsAsync(applicationId, cancellationToken);

        if (!exists)
        {
            return NotFoundProblem($"Loan application '{applicationId}' was not found.");
        }

        var events = await workflow.GetTimelineAsync(applicationId, cancellationToken);

        return TypedResults.Ok<IReadOnlyCollection<DomainEventResponse>>([.. events.Select(ApplicationMappings.ToResponse)]);
    }

    private static async Task<Results<Ok<LoanDecisionResponse>, NotFound<ProblemDetails>>> GetDecisionAsync(
        string applicationId,
        ApplicationWorkflowService workflow,
        CancellationToken cancellationToken)
    {
        var application = await workflow.GetApplicationAsync(applicationId, cancellationToken);

        if (application is null)
        {
            return NotFoundProblem($"Loan application '{applicationId}' was not found.");
        }

        return TypedResults.Ok(application.ToDecisionResponse());
    }

    private static NotFound<ProblemDetails> NotFoundProblem(string detail) =>
        TypedResults.NotFound(new ProblemDetails
        {
            Title = "Not found",
            Detail = detail,
            Status = StatusCodes.Status404NotFound
        });
}
