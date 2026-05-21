namespace AuthApi.Contracts.Responses;

public record CustomerProfileResponse(
    string CustomerId,
    string Login,
    string DisplayName,
    string? Email,
    string? PhoneNumber,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? Country,
    string? PostalCode,
    bool MarketingOptIn,
    bool AgreementsAccepted,
    DateTime? AgreementsAcceptedAtUtc,
    TenantSummary Tenant);
