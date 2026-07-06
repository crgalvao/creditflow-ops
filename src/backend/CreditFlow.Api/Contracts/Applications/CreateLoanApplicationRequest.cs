namespace CreditFlow.Api.Contracts.Applications;

public sealed record CreateLoanApplicationRequest(
    string OwnerUserId,
    BorrowerRequest Borrower,
    decimal RequestedAmount,
    decimal MonthlyRevenue,
    string Currency)
{
    private const decimal MinimumRequestedAmount = 5_000m;
    private const decimal MaximumRequestedAmount = 500_000m;
    private const decimal MinimumMonthlyRevenue = 10_000m;
    private const decimal MaximumRequestToAnnualRevenueRatio = 0.50m;

    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(OwnerUserId))
        {
            AddError(errors, nameof(OwnerUserId), "Owner user id is required.");
        }

        if (OwnerUserId?.Length > 120)
        {
            AddError(errors, nameof(OwnerUserId), "Owner user id must not exceed 120 characters.");
        }

        if (Borrower is null)
        {
            AddError(errors, nameof(Borrower), "Borrower is required.");
        }
        else
        {
            Borrower.AddValidationErrors(errors);
        }

        if (RequestedAmount < MinimumRequestedAmount)
        {
            AddError(
                errors,
                nameof(RequestedAmount),
                $"Requested amount must be at least {MinimumRequestedAmount:N0}.");
        }

        if (RequestedAmount > MaximumRequestedAmount)
        {
            AddError(
                errors,
                nameof(RequestedAmount),
                $"Requested amount must not exceed {MaximumRequestedAmount:N0}.");
        }

        if (MonthlyRevenue < MinimumMonthlyRevenue)
        {
            AddError(
                errors,
                nameof(MonthlyRevenue),
                $"Monthly revenue must be at least {MinimumMonthlyRevenue:N0}.");
        }

        if (MonthlyRevenue > 0)
        {
            var annualRevenue = MonthlyRevenue * 12m;
            var requestToAnnualRevenueRatio = RequestedAmount / annualRevenue;

            if (requestToAnnualRevenueRatio > MaximumRequestToAnnualRevenueRatio)
            {
                AddError(
                    errors,
                    nameof(RequestedAmount),
                    "Requested amount is too high relative to annual revenue for pre-qualification.");
            }
        }

        if (string.IsNullOrWhiteSpace(Currency))
        {
            AddError(errors, nameof(Currency), "Currency is required.");
        }
        else if (!IsValidCurrencyCode(Currency))
        {
            AddError(errors, nameof(Currency), "Currency must be a 3-letter ISO-style currency code.");
        }

        return errors.ToDictionary(
            static item => item.Key,
            static item => item.Value.ToArray(),
            StringComparer.Ordinal);
    }

    private static bool IsValidCurrencyCode(string currency)
    {
        return currency.Length == 3 && currency.All(static character => character is >= 'A' and <= 'Z');
    }

    internal static void AddError(
        Dictionary<string, List<string>> errors,
        string field,
        string message)
    {
        if (!errors.TryGetValue(field, out var fieldErrors))
        {
            fieldErrors = [];
            errors[field] = fieldErrors;
        }

        fieldErrors.Add(message);
    }
}

public sealed record BorrowerRequest(
    string LegalName,
    string TaxId,
    string Industry,
    int MonthsInBusiness)
{
    private const int MinimumMonthsInBusiness = 12;
    private const int MaximumMonthsInBusiness = 600;

    internal void AddValidationErrors(Dictionary<string, List<string>> errors)
    {
        if (string.IsNullOrWhiteSpace(LegalName))
        {
            CreateLoanApplicationRequest.AddError(
                errors,
                "borrower.legalName",
                "Borrower legal name is required.");
        }
        else if (LegalName.Length > 160)
        {
            CreateLoanApplicationRequest.AddError(
                errors,
                "borrower.legalName",
                "Borrower legal name must not exceed 160 characters.");
        }

        if (string.IsNullOrWhiteSpace(TaxId))
        {
            CreateLoanApplicationRequest.AddError(
                errors,
                "borrower.taxId",
                "Borrower tax id is required.");
        }
        else if (TaxId.Length > 80)
        {
            CreateLoanApplicationRequest.AddError(
                errors,
                "borrower.taxId",
                "Borrower tax id must not exceed 80 characters.");
        }

        if (string.IsNullOrWhiteSpace(Industry))
        {
            CreateLoanApplicationRequest.AddError(
                errors,
                "borrower.industry",
                "Borrower industry is required.");
        }
        else if (Industry.Length > 80)
        {
            CreateLoanApplicationRequest.AddError(
                errors,
                "borrower.industry",
                "Borrower industry must not exceed 80 characters.");
        }

        if (MonthsInBusiness < MinimumMonthsInBusiness)
        {
            CreateLoanApplicationRequest.AddError(
                errors,
                "borrower.monthsInBusiness",
                $"Borrower must be in business for at least {MinimumMonthsInBusiness} months.");
        }

        if (MonthsInBusiness > MaximumMonthsInBusiness)
        {
            CreateLoanApplicationRequest.AddError(
                errors,
                "borrower.monthsInBusiness",
                $"Borrower months in business must not exceed {MaximumMonthsInBusiness}.");
        }
    }
}
