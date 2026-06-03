using AuthApi.Models;
using Microsoft.EntityFrameworkCore;
using Continuo.Shared.Contracts;

namespace AuthApi.Services.Wizard;

/// <summary>
/// EF-backed implementation of <see cref="IDefaultRoleSeederService"/>.
///
/// Behaviour:
/// <list type="bullet">
/// <item>Names roles as <c>{tenantSlug}.{role.Code}</c> so multiple tenants
///     can use the same logical role code (e.g. "manager") without colliding
///     in the global Roles table. This is a stop-gap until the Role entity
///     gains a real <c>TenantCode</c> column.</item>
/// <item><see cref="Role.Scope"/> = <see cref="RoleScope.Tenant"/>,
///     <see cref="Role.IsSystem"/> = <c>true</c> — the admin UI hides
///     System roles from delete actions, so wizard-seeded rows are
///     protected from accidental drift.</item>
/// <item>Permission keys that don't exist in the <see cref="Permission"/>
///     catalog are reported in
///     <see cref="DefaultRoleSeederResult.MissingPermissionKeys"/> rather
///     than throwing — the wizard caller can route those to the admin
///     alert. Unknown keys never reach the <c>RolePermissions</c> table
///     so the FK never breaks.</item>
/// </list>
///
/// Idempotency: this service is replay-safe (lookup-then-upsert per role),
/// the consumer wraps it in an inbox check on RequestId for an extra
/// safety net so MT redeliveries don't even reach the DB twice.
/// </summary>
public sealed class DefaultRoleSeederService : IDefaultRoleSeederService {
    private readonly AuthDbContext _db;
    private readonly ILogger<DefaultRoleSeederService> _log;

    public DefaultRoleSeederService(AuthDbContext db, ILogger<DefaultRoleSeederService> log) {
        _db = db;
        _log = log;
    }

    public async Task<DefaultRoleSeederResult> SeedAsync(
        DefaultRoleSeedRequestedEvent message,
        CancellationToken cancellationToken) {
        if (string.IsNullOrWhiteSpace(message.TenantSlug)) {
            throw new ArgumentException("TenantSlug is required.", nameof(message));
        }

        var inserted = 0;
        var updated = 0;
        var skipped = 0;
        var missing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Pre-load the entire permission catalog once — much cheaper than
        // a per-role lookup. Keys are unique so no Where clause needed.
        var validPermissions = await _db.Permissions
            .AsNoTracking()
            .Select(p => p.Key)
            .ToListAsync(cancellationToken);
        var validSet = new HashSet<string>(validPermissions, StringComparer.OrdinalIgnoreCase);

        foreach (var seed in message.Roles) {
            if (string.IsNullOrWhiteSpace(seed.Code)) {
                skipped++;
                continue;
            }

            var canonicalName = $"{message.TenantSlug.Trim().ToLowerInvariant()}.{seed.Code.Trim()}";
            var existing = await _db.Roles
                .Include(r => r.Permissions)
                .FirstOrDefaultAsync(r => r.Name == canonicalName, cancellationToken);

            Role role;
            if (existing is null) {
                role = new Role {
                    Name = canonicalName,
                    Description = seed.DisplayName,
                    Scope = RoleScope.Tenant,
                    IsSystem = true
                };
                _db.Roles.Add(role);
                inserted++;
            }
            else {
                role = existing;
                role.Description = seed.DisplayName ?? role.Description;
                role.Scope = RoleScope.Tenant;
                role.IsSystem = true;
                updated++;
            }

            UpsertRolePermissions(role, seed.PermissionCodes, validSet, missing);
        }

        if (inserted + updated > 0) {
            await _db.SaveChangesAsync(cancellationToken);
        }

        var result = new DefaultRoleSeederResult(
            InsertedRoles: inserted,
            UpdatedRoles: updated,
            SkippedRoles: skipped,
            MissingPermissionKeys: missing.OrderBy(x => x).ToArray());

        if (missing.Count > 0) {
            _log.LogWarning(
                "DefaultRoleSeederService: tenant {Slug} seed dropped {Count} unknown permission keys: {Keys}",
                message.TenantSlug, missing.Count, string.Join(",", result.MissingPermissionKeys));
        }
        return result;
    }

    private static void UpsertRolePermissions(
        Role role,
        IReadOnlyList<string> requestedKeys,
        HashSet<string> validKeys,
        HashSet<string> missingSink) {
        var keepSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in requestedKeys) {
            if (string.IsNullOrWhiteSpace(key)) continue;
            if (!validKeys.Contains(key)) {
                missingSink.Add(key);
                continue;
            }
            keepSet.Add(key);
        }

        // Add missing
        foreach (var key in keepSet) {
            if (!role.Permissions.Any(p => string.Equals(p.PermissionKey, key, StringComparison.OrdinalIgnoreCase))) {
                role.Permissions.Add(new RolePermission { PermissionKey = key });
            }
        }
        // Remove ones not in the new payload — keeps role definition canonical
        // for the seed source. Manual permissions a tenant admin added stay
        // when their key is in the wizard set; otherwise they get reset by
        // the next wizard run (acceptable for V2.0 seed behaviour).
        var toRemove = role.Permissions
            .Where(p => !keepSet.Contains(p.PermissionKey))
            .ToList();
        foreach (var rp in toRemove) {
            role.Permissions.Remove(rp);
        }
    }
}
