using AuthApi.Models;
using Continuo.Shared.Security;

namespace AuthApi.Infrastructure;

public readonly record struct TenantBranchActorScope(
    bool IsOwnerBypass,
    bool IsConsoleAdminRequest,
    string? TenantCode,
    IReadOnlySet<string> BranchCodes) {
    public bool RequiresTenantScope => IsConsoleAdminRequest && !IsOwnerBypass;
    public bool RequiresBranchScope => IsConsoleAdminRequest && !IsOwnerBypass;
}

public static class TenantBranchAuthorization {
    private static readonly string[] OwnerRoles = { "PlatformOwner", "TenantOwner" };

    public static TenantBranchActorScope Resolve(HttpContext context, ITenantContext tenantContext, IConfiguration configuration) {
        var ownerLogin = configuration["AUTH_OWNER_LOGIN"] ?? "platform.owner@example.local";
        var isOwnerBypass = ClaimsHelper.IsOwnerLogin(context.User, ownerLogin) || ClaimsHelper.HasAnyRole(context, OwnerRoles);

        var clientApp = context.Request.Headers["X-Client-App"].FirstOrDefault();
        var isConsoleAdminRequest = string.Equals(clientApp, "console-admin", StringComparison.OrdinalIgnoreCase);

        var tenantCode = NormalizeTenantCode(tenantContext.TenantCode);
        if (string.IsNullOrWhiteSpace(tenantCode)) {
            tenantCode = ClaimsHelper.GetTenantCodes(context.User)
                .Select(NormalizeTenantCode)
                .FirstOrDefault(code => !string.IsNullOrWhiteSpace(code));
        }

        var branchCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var branchCode in ClaimsHelper.GetBranchCodes(context.User).Select(NormalizeBranchCode)) {
            if (!string.IsNullOrWhiteSpace(branchCode)) {
                branchCodes.Add(branchCode);
            }
        }

        return new TenantBranchActorScope(
            IsOwnerBypass: isOwnerBypass,
            IsConsoleAdminRequest: isConsoleAdminRequest,
            TenantCode: tenantCode,
            BranchCodes: branchCodes);
    }

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

    public static bool IsBranchAllowed(TenantBranchActorScope scope, string? branchCode, bool rejectEmptyCodes = true) {
        if (scope.IsOwnerBypass) {
            return true;
        }

        var normalized = NormalizeBranchCode(branchCode);
        if (string.IsNullOrWhiteSpace(normalized)) {
            return !rejectEmptyCodes;
        }

        return scope.BranchCodes.Contains(normalized);
    }

    public static bool AreBranchesAllowed(TenantBranchActorScope scope, IEnumerable<string?> branchCodes, bool rejectEmptyCodes = true) {
        if (scope.IsOwnerBypass) {
            return true;
        }

        var hasAny = false;
        foreach (var branchCode in branchCodes) {
            hasAny = true;
            if (!IsBranchAllowed(scope, branchCode, rejectEmptyCodes)) {
                return false;
            }
        }

        return hasAny || !rejectEmptyCodes;
    }

    public static bool IsTenantAllowed(TenantBranchActorScope scope, string? tenantCode) {
        if (scope.IsOwnerBypass) {
            return true;
        }

        if (string.IsNullOrWhiteSpace(scope.TenantCode)) {
            return false;
        }

        return IsTenantMatch(scope.TenantCode, tenantCode);
    }

    public static bool IsTenantUserInScope(TenantBranchActorScope scope, TenantUser user) {
        if (scope.IsOwnerBypass) {
            return true;
        }

        if (!IsTenantAllowed(scope, user.Tenant?.Code)) {
            return false;
        }

        if (scope.BranchCodes.Count == 0) {
            return !scope.RequiresBranchScope;
        }

        if (user.Roles.Any(role => string.IsNullOrWhiteSpace(role.BranchCode))) {
            return false;
        }

        var targetBranches = user.Roles
            .Select(role => NormalizeBranchCode(role.BranchCode))
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (targetBranches.Length == 0) {
            return false;
        }

        return targetBranches.All(scope.BranchCodes.Contains);
    }
}
