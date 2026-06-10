namespace AuthApi.Infrastructure;

/// <summary>
/// Read-only value type representing the resolved tenant/branch authorization
/// scope of the current actor. Phase-1 move-only extraction from
/// <see cref="TenantBranchAuthorization"/>.
/// </summary>
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
