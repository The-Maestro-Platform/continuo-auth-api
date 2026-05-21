using AuthApi.Models;

namespace AuthApi.Services;

public record TwoFactorVerificationResult(bool Success, string? Error, Credential? Credential) {
    public static TwoFactorVerificationResult Invalid(string error) => new(false, error, null);
    public static TwoFactorVerificationResult FromCredential(Credential credential) => new(true, null, credential);
}
