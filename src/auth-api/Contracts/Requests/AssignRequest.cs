namespace AuthApi.Contracts.Requests;

public record AssignRequest(string ScreenId, string PlatformUserId, string? TenantId, DateTime? ExpiresAtUtc);
