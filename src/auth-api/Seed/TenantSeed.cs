namespace AuthApi.Seed;

public record TenantSeed(string Code, string Name, string Slug, string Subdomain, string? Email, string? Phone, string? Notes);
