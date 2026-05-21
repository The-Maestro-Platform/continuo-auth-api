namespace AuthApi.Services;

public record TwoFactorResendResult(bool Success, string? Error, DateTime? ExpiresAtUtc, DateTime? RetryAfterUtc) {
    public static TwoFactorResendResult Ok(DateTime expiresAtUtc) => new(true, null, expiresAtUtc, null);
    public static TwoFactorResendResult Invalid(string error) => new(false, error, null, null);
    public static TwoFactorResendResult RateLimited(DateTime retryAfterUtc) => new(false, "ResendCooldown", null, retryAfterUtc);
}
