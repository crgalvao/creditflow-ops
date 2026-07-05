using CreditFlow.Domain.SharedKernel;

namespace CreditFlow.Domain.Applications.ValueObjects;

public sealed record BorrowerSnapshot(
    string LegalName,
    string TaxId,
    string Industry,
    int MonthsInBusiness)
{
    public static BorrowerSnapshot Create(
        string legalName,
        string taxId,
        string industry,
        int monthsInBusiness)
    {
        return new BorrowerSnapshot(
            Guard.Required(legalName, nameof(legalName), 160),
            Guard.Required(taxId, nameof(taxId), 80),
            Guard.Required(industry, nameof(industry), 80),
            Guard.InRange(monthsInBusiness, nameof(monthsInBusiness), 0, 600));
    }
}
