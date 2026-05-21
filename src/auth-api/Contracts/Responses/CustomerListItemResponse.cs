namespace AuthApi.Contracts.Responses;

public record CustomerListItemResponse(
    string CustomerId,
    string? Login,
    string? DisplayName,
    string? FullName,
    string? Email,
    string? PhoneNumber,
    string? City,
    bool MarketingOptIn,
    DateTime CreatedAtUtc,
    bool IsActive);
