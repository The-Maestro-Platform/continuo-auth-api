namespace AuthApi.Contracts.Requests;

public record AcceptAgreementsRequest(string? Login, string? Version, bool MarketingOptIn);
