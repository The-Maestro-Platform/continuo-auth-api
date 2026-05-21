namespace AuthApi.Contracts.Requests;

public record CreateTenantRequest(string Code, string Name, string? Subdomain, string? ContactEmail, string? ContactPhone);
