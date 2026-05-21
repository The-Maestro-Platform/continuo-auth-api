namespace AuthApi.Contracts.Responses;

public record CustomerSummaryResponse(
    string CustomerId,
    string? Login,
    string? Email,
    string? DisplayName,
    string? FullName
);

