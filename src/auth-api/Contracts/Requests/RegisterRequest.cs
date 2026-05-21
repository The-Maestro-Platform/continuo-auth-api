namespace AuthApi.Contracts.Requests;

public record RegisterRequest(
    string Email,
    string Password,
    string? DisplayName,
    string? PhoneNumber,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? Country,
    string? PostalCode,
    string? TenantCode,
    bool MarketingOptIn);
