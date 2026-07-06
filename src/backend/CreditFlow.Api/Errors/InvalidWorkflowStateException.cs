namespace CreditFlow.Api.Errors;

public sealed class InvalidWorkflowStateException(string message) : Exception(message);
