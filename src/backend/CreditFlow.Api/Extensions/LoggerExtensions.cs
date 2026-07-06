namespace CreditFlow.Api.Extensions;

public static partial class LoggerExtensions
{
    // EventId 1-99: System/Infrastructure Errors
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "An unexpected system error occurred while processing the request.")]
    public static partial void LogUnexpectedSystemError(this ILogger logger, Exception ex);

   // EventId 100-199: Domain/Business Logic Warnings
    [LoggerMessage(
        EventId = 100,
        Level = LogLevel.Warning,
        Message = "Business rule violation: {ViolationReason}")]
    public static partial void LogBusinessRuleViolation(this ILogger logger, Exception ex, string violationReason);

   // EventId 200-299: Domain Events & Tracing
    [LoggerMessage(
        EventId = 200,
        Level = LogLevel.Information,
        Message = "Published domain event {EventType} for application {ApplicationId}. EventId: {EventId}. CorrelationId: {CorrelationId}")]
    public static partial void LogDomainEventPublished(
        this ILogger logger,
        string eventType,
        string applicationId,
        Guid eventId,
        string correlationId);
}
