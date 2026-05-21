namespace AuthApi.Contracts.Responses;

public record CustomerSearchItemResponse(
    string CustomerId,
    string? DisplayName,
    string? FullName,
    string? Email,
    string? PhoneNumber);

