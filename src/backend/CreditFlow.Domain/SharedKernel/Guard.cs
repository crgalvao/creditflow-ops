using System;

namespace CreditFlow.Domain.SharedKernel;

public static class Guard
{
    public static string Required(
        string? value,
        string fieldName,
        int maxLength = 250)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, fieldName);

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new DomainException(
                $"{fieldName} must not exceed {maxLength} characters.");
        }

        return normalized;
    }

    public static int InRange(
        int value,
        string fieldName,
        int min,
        int max) =>
        value >= min && value <= max
            ? value
            : throw new DomainException(
                $"{fieldName} must be between {min} and {max}.");
}
