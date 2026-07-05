using CreditFlow.Domain.Kyc.Enums;

namespace CreditFlow.Domain.Kyc.ValueObjects;

public sealed record KycSnapshot(
    string ClientId,
    string TaxId,
    string LegalName,
    KycStatus Status,
    IReadOnlyCollection<string> RiskFlags,
    int Version,
    DateTimeOffset CheckedAt);
