using CreditFlow.Domain.CreditDecisioning.Enums;
using CreditFlow.Domain.SharedKernel;

namespace CreditFlow.Domain.CreditDecisioning.ValueObjects;

public sealed record CreditProductOffer(
    CreditProductCode ProductCode,
    string Name,
    Money MaxAmount,
    int TermMonths,
    decimal MonthlyInterestRate)
{
    public static CreditProductOffer Create(
        CreditProductCode productCode,
        string name,
        Money maxAmount,
        int termMonths,
        decimal monthlyInterestRate)
    {
        if (monthlyInterestRate <= 0)
        {
            throw new DomainException("Monthly interest rate must be greater than zero.");
        }

        return new CreditProductOffer(
            productCode,
            Guard.Required(name, nameof(name), 120),
            maxAmount,
            Guard.InRange(termMonths, nameof(termMonths), 1, 120),
            monthlyInterestRate);
    }
}
