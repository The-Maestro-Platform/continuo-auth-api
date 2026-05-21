namespace AuthApi.Services;

public sealed record TwoFactorDispatchPayload(
    string ChallengeId,
    string Channel,
    string Target,
    string Code,
    DateTime ExpiresAtUtc,
    string? DisplayName,
    string? TenantName,
    string? Flow);

