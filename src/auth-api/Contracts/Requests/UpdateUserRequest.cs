namespace AuthApi.Contracts.Requests;

public record UpdateUserRequest(
    string? DisplayName,
    string? FirstName,
    string? LastName,
    string? Email,
    string? PhoneNumber,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? Country,
    string? PostalCode,
    string? PositionTitle,
    bool? MarketingOptIn);
