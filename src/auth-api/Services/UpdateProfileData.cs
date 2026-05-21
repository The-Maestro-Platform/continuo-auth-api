namespace AuthApi.Services;

public record UpdateProfileData(
    string? DisplayName,
    string? FullName,
    string? PhoneNumber,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? Country,
    string? PostalCode,
    bool? MarketingOptIn);
