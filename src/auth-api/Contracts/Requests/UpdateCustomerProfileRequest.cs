namespace AuthApi.Contracts.Requests;

public record UpdateCustomerProfileRequest(
    string? DisplayName,
    string? FullName,
    string? PhoneNumber,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? Country,
    string? PostalCode,
    bool? MarketingOptIn);
