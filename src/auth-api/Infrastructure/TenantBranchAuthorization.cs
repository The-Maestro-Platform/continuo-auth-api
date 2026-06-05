using AuthApi.Models;
using Continuo.Shared.Security;

namespace AuthApi.Infrastructure;

public readonly record struct TenantBranchActorScope(
    bool IsPlatformBypass,
    bool IsOwnerBypass,
    bool IsConsoleAdminRequest,
    string? TenantCode,
    IReadOnlySet<string> TenantCodes,
    IReadOnlySet<string> BranchCodes) {
    public bool RequiresTenantScope => IsConsoleAdminRequest && !IsOwnerBypass;
    public bool RequiresBranchScope => IsConsoleAdminRequest && !IsOwnerBypass;
}

public static class TenantBranchAuthorization {
    // Platform-level actors may cross tenant boundaries (manage any tenant's users/roles).
    private static readonly PlatformRole[] PlatformBypassRoles = { PlatformRole.PlatformOwner, PlatformRole.PlatformAdmin };

    public static TenantBranchActorScope Resolve(HttpContext context, ITenantContext tenantContext, IConfiguration configuration) {
        var ownerLogin = configuration["AUTH_OWNER_LOGIN"] ?? "platform.owner@example.local";
        // IsPlatformBypass: cross-tenant authority (platform owner/admin or the configured owner login).
        var isPlatformBypass = ClaimsHelper.IsOwnerLogin(context.User, ownerLogin)
            || ClaimsHelper.HasAnyRole(context, PlatformBypassRoles);
        // IsOwnerBypass: branch-level bypass. A TenantOwner manages every branch *within their own
        // tenant* but must NOT see or touch other tenants — tenant scoping below still applies to them.
        var isOwnerBypass = isPlatformBypass || ClaimsHelper.HasAnyRole(context, PlatformRole.TenantOwner);

        var clientApp = context.Request.Headers["X-Client-App"].FirstOrDefault();
        var isConsoleAdminRequest = string.Equals(clientApp, "console-admin", StringComparison.OrdinalIgnoreCase);

        // Collect every tenant identifier the actor carries (the JWT holds both tenant_code "t-001"
        // and tenant_slug "default"); match against either so slug/code mismatches don't lock out
        // a legitimate tenant admin.
        var tenantCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var contextTenant = NormalizeTenantCode(tenantContext.TenantCode);
        if (!string.IsNullOrWhiteSpace(contextTenant)) {
            tenantCodes.Add(contextTenant);
        }
        foreach (var code in ClaimsHelper.GetTenantCodes(context.User).Select(NormalizeTenantCode)) {
            if (!string.IsNullOrWhiteSpace(code)) {
                tenantCodes.Add(code!);
            }
        }

        var tenantCode = contextTenant;
        if (string.IsNullOrWhiteSpace(tenantCode)) {
            tenantCode = tenantCodes.FirstOrDefault();
        }

        var branchCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var branchCode in ClaimsHelper.GetBranchCodes(context.User).Select(NormalizeBranchCode)) {
            if (!string.IsNullOrWhiteSpace(branchCode)) {
                branchCodes.Add(branchCode);
            }
        }

        return new TenantBranchActorScope(
            IsPlatformBypass: isPlatformBypass,
            IsOwnerBypass: isOwnerBypass,
            IsConsoleAdminRequest: isConsoleAdminRequest,
            TenantCode: tenantCode,
            TenantCodes: tenantCodes,
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
        // Only platform-level actors cross tenants. TenantOwner is NOT a tenant bypass here —
        // their own tenant must appear in the actor's tenant identifier set.
        if (scope.IsPlatformBypass) {
            return true;
        }

        var target = NormalizeTenantCode(tenantCode);
        if (string.IsNullOrWhiteSpace(target)) {
            return false;
        }

        if (scope.TenantCodes.Count > 0) {
            return scope.TenantCodes.Any(code => IsTenantMatch(code, target));
        }

        return IsTenantMatch(scope.TenantCode, target);
    }

    public static bool IsTenantUserInScope(TenantBranchActorScope scope, TenantUser user) {
        if (scope.IsPlatformBypass) {
            return true;
        }

        // Match either the tenant's canonical code or its slug — the actor's claims may carry
        // whichever form, and a TenantOwner must remain locked to their own tenant.
        if (!IsTenantAllowed(scope, user.Tenant?.Code) && !IsTenantAllowed(scope, user.Tenant?.Slug)) {
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
