namespace CreditFlow.Api.Contracts.Applications;

public sealed record CreateLoanApplicationRequest(
    string OwnerUserId,
    BorrowerRequest Borrower,
    decimal RequestedAmount,
    decimal MonthlyRevenue,
    string Currency)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(OwnerUserId))
        {
            errors[nameof(OwnerUserId)] = ["Owner user id is required."];
        }

        if (Borrower is null)
        {
            errors[nameof(Borrower)] = ["Borrower is required."];
        }
        else
        {
            Borrower.AddValidationErrors(errors);
        }

        if (RequestedAmount <= 0)
        {
            errors[nameof(RequestedAmount)] = ["Requested amount must be greater than zero."];
        }

        if (MonthlyRevenue < 0)
        {
            errors[nameof(MonthlyRevenue)] = ["Monthly revenue cannot be negative."];
        }

        if (string.IsNullOrWhiteSpace(Currency))
        {
            errors[nameof(Currency)] = ["Currency is required."];
        }

        return errors;
    }
}

public sealed record BorrowerRequest(
    string LegalName,
    string TaxId,
    string Industry,
    int MonthsInBusiness)
{
    internal void AddValidationErrors(Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(LegalName))
        {
            errors["borrower.legalName"] = ["Borrower legal name is required."];
        }

        if (string.IsNullOrWhiteSpace(TaxId))
        {
            errors["borrower.taxId"] = ["Borrower tax id is required."];
        }

        if (string.IsNullOrWhiteSpace(Industry))
        {
            errors["borrower.industry"] = ["Borrower industry is required."];
        }

        if (MonthsInBusiness < 0)
        {
            errors["borrower.monthsInBusiness"] = ["Months in business cannot be negative."];
        }
    }
}
