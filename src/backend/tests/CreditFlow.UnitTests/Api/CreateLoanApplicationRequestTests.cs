using CreditFlow.Api.Contracts.Applications;

namespace CreditFlow.UnitTests.Api;

public sealed class CreateLoanApplicationRequestTests
{
    [Fact]
    public void Validate_WhenRequestIsValid_ReturnsNoErrors()
    {
        var request = new CreateLoanApplicationRequest(
            OwnerUserId: "user-001",
            Borrower: new BorrowerRequest(
                LegalName: "Demo Coffee Imports Ltd",
                TaxId: "DEMO-001",
                Industry: "Wholesale",
                MonthsInBusiness: 84),
            RequestedAmount: 50_000m,
            MonthlyRevenue: 90_000m,
            Currency: "USD");

        var errors = request.Validate();

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WhenRequiredFieldsAreMissing_ReturnsValidationErrors()
    {
        var request = new CreateLoanApplicationRequest(
            OwnerUserId: "",
            Borrower: new BorrowerRequest(
                LegalName: "",
                TaxId: "",
                Industry: "",
                MonthsInBusiness: 0),
            RequestedAmount: 0m,
            MonthlyRevenue: 0m,
            Currency: "");

        var errors = request.Validate();

        Assert.Contains(nameof(CreateLoanApplicationRequest.OwnerUserId), errors.Keys);
        Assert.Contains("borrower.legalName", errors.Keys);
        Assert.Contains("borrower.taxId", errors.Keys);
        Assert.Contains("borrower.industry", errors.Keys);
        Assert.Contains("borrower.monthsInBusiness", errors.Keys);
        Assert.Contains(nameof(CreateLoanApplicationRequest.RequestedAmount), errors.Keys);
        Assert.Contains(nameof(CreateLoanApplicationRequest.MonthlyRevenue), errors.Keys);
        Assert.Contains(nameof(CreateLoanApplicationRequest.Currency), errors.Keys);
    }

    [Fact]
    public void Validate_WhenRequestedAmountIsTooHighRelativeToRevenue_ReturnsPrequalificationError()
    {
        var request = new CreateLoanApplicationRequest(
            OwnerUserId: "user-001",
            Borrower: new BorrowerRequest(
                LegalName: "Over Requested Ltd",
                TaxId: "DEMO-HIGH-001",
                Industry: "Retail",
                MonthsInBusiness: 36),
            RequestedAmount: 250_000m,
            MonthlyRevenue: 20_000m,
            Currency: "USD");

        var errors = request.Validate();

        Assert.Contains(nameof(CreateLoanApplicationRequest.RequestedAmount), errors.Keys);
        Assert.Contains(
            errors[nameof(CreateLoanApplicationRequest.RequestedAmount)],
            message => message.Contains("relative to annual revenue", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("usd")]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("12D")]
    public void Validate_WhenCurrencyIsInvalid_ReturnsCurrencyError(string currency)
    {
        var request = new CreateLoanApplicationRequest(
            OwnerUserId: "user-001",
            Borrower: new BorrowerRequest(
                LegalName: "Demo Ltd",
                TaxId: "DEMO-001",
                Industry: "Services",
                MonthsInBusiness: 24),
            RequestedAmount: 20_000m,
            MonthlyRevenue: 50_000m,
            Currency: currency);

        var errors = request.Validate();

        Assert.Contains(nameof(CreateLoanApplicationRequest.Currency), errors.Keys);
    }
}
