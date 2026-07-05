using System;

namespace CreditFlow.Domain.SharedKernel;

public sealed class DomainException(string message) : Exception(message);
