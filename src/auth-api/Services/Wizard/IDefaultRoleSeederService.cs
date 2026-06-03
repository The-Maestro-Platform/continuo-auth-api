using Continuo.Shared.Contracts;

namespace AuthApi.Services.Wizard;

/// <summary>
/// V2 Wizard Phase F4 — persists the package-aware default role + permission
/// map shipped by tenant-api during provisioning. Lives next to
/// <see cref="RolesService"/> instead of inside it because the bootstrap
/// path needs explicit tenant scoping (consumer runs outside an HTTP scope)
/// and a different idempotency contract from interactive role CRUD.
/// </summary>
public interface IDefaultRoleSeederService {
    Task<DefaultRoleSeederResult> SeedAsync(
        DefaultRoleSeedRequestedEvent message,
        CancellationToken cancellationToken);
}

public sealed record DefaultRoleSeederResult(
    int InsertedRoles,
    int UpdatedRoles,
    int SkippedRoles,
    IReadOnlyList<string> MissingPermissionKeys);
