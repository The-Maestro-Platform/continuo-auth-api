using Continuo.Shared.Security;

namespace AuthApi.Infrastructure;

/// <summary>
/// Pure normalization/comparison helpers for tenant and branch codes.
/// Phase-1 move-only extraction from <see cref="TenantBranchAuthorization"/>;
/// the original public methods forward here so the existing public API is
/// preserved.
/// </summary>
public static class TenantBranchNormalizers {
    public static string? NormalizeTenantCode(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }

        return value.Trim().ToLowerInvariant();
    }

    public static string? NormalizeBranchCode(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }

        return value.Trim().ToLowerInvariant();
    }

    public static bool IsTenantMatch(string? left, string? right) {
        return TenantResolution.IsTenantMatch(left, right);
    }

    public static bool IsBranchMatch(string? left, string? right) {
        return TenantResolution.IsBranchMatch(left, right);
    }
}
