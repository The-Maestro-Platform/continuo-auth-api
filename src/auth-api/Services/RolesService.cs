using AuthApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Services;

public class RolesService {
    private readonly AuthDbContext _db;
    public RolesService(AuthDbContext db) { _db = db; }

    public async Task<IReadOnlyList<object>> ListAsync(int take, RoleScope? scope, bool includeScreens, CancellationToken ct) {
        var size = Continuo.Shared.Pagination.Paging.NormalizePageSize(take, 500, 10, 5000);
        IQueryable<Role> query = _db.Roles
            .Include(r => r.Permissions)
                .ThenInclude(rp => rp.Permission)
            .Include(r => r.ScreenAssignments)
            .AsNoTracking();

        if (scope.HasValue) {
            query = query.Where(r => r.Scope == scope.Value);
        }

        var roles = await query
            .OrderBy(r => r.Name)
            .Take(size)
            .ToListAsync(ct);

        return roles.Select(r => new {
            id = r.Id.ToString(),
            r.Name,
            r.Description,
            scope = r.Scope,
            isSystem = r.IsSystem,
            permissions = r.Permissions.Select(p => p.PermissionKey),
            screens = includeScreens
                ? r.ScreenAssignments.Select(sr => sr.ScreenId.ToString()).ToArray()
                : Array.Empty<string>()
        } as object).ToList();
    }

    public async Task<RoleScope?> GetScopeAsync(Ulid roleId, CancellationToken ct) {
        return await _db.Roles
            .AsNoTracking()
            .Where(r => r.Id == roleId)
            .Select(r => (RoleScope?)r.Scope)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<object> CreateAsync(string name, RoleScope scope, string? description, string[]? permissionKeys, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(name)) {
            throw new ArgumentException("Name is required", nameof(name));
        }

        name = name.Trim();
        var exists = await _db.Roles.AnyAsync(r => r.Name == name, ct);
        if (exists) {
            throw new InvalidOperationException("Role already exists");
        }

        var role = new Role {
            Name = name,
            Scope = scope,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim()
        };

        if (permissionKeys is { Length: > 0 }) {
            var keys = permissionKeys
                .Select(k => k.Trim())
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var allowed = await _db.Permissions.Where(p => keys.Contains(p.Key)).Select(p => p.Key).ToListAsync(ct);
            foreach (var key in allowed) {
                role.Permissions.Add(new RolePermission {
                    Role = role,
                    PermissionKey = key
                });
            }
        }

        _db.Roles.Add(role);
        await _db.SaveChangesAsync(ct);
        return new { id = role.Id.ToString(), role.Name };
    }

    public async Task<IReadOnlyList<string>> SetScreensAsync(Ulid roleId, string[] screenIds, CancellationToken ct) {
        var role = await _db.Roles
            .Include(r => r.ScreenAssignments)
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == roleId, ct);
        if (role == null) {
            throw new InvalidOperationException("Role not found");
        }

        var parsedScreenIds = screenIds
            .Where(id => Ulid.TryParse(id, out _))
            .Select(Ulid.Parse)
            .Distinct()
            .ToArray();

        var screens = await _db.Screens
            .Where(s => parsedScreenIds.Contains(s.Id))
            .Select(s => new { s.Id, required = s.RequiredPermissions })
            .ToListAsync(ct);

        _db.ScreenRoles.RemoveRange(role.ScreenAssignments);
        role.ScreenAssignments.Clear();

        foreach (var screenId in screens) {
            role.ScreenAssignments.Add(new ScreenRole { ScreenId = screenId.Id, RoleId = role.Id });
        }

        // Auto-grant permissions required by selected screens.
        var requiredPermissionKeys = screens
            .SelectMany(screen => screen.required)
            .Select(key => key.Trim())
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var resolvedRequiredKeys = requiredPermissionKeys.Length == 0
            ? Array.Empty<string>()
            : await _db.Permissions
                .Where(permission => requiredPermissionKeys.Contains(permission.Key) && permission.Scope == role.Scope)
                .Select(permission => permission.Key)
                .ToArrayAsync(ct);

        var existingRolePermissionKeys = role.Permissions
            .Select(permission => permission.PermissionKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newlyAddedPermissionKeys = new List<string>();
        foreach (var permissionKey in resolvedRequiredKeys) {
            if (existingRolePermissionKeys.Contains(permissionKey)) {
                continue;
            }

            role.Permissions.Add(new RolePermission { RoleId = role.Id, PermissionKey = permissionKey });
            existingRolePermissionKeys.Add(permissionKey);
            newlyAddedPermissionKeys.Add(permissionKey);
        }

        await _db.SaveChangesAsync(ct);
        return newlyAddedPermissionKeys;
    }

    public async Task<IReadOnlyList<string>> SetPermissionsAsync(Ulid roleId, string[] permissionKeys, CancellationToken ct) {
        var role = await _db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == roleId, ct);
        if (role == null) {
            throw new InvalidOperationException("Role not found");
        }

        var requestedKeys = (permissionKeys ?? Array.Empty<string>())
            .Select(k => k.Trim())
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var allowed = requestedKeys.Length == 0
            ? Array.Empty<string>()
            : await _db.Permissions.Where(p => requestedKeys.Contains(p.Key)).Select(p => p.Key).ToArrayAsync(ct);

        _db.RolePermissions.RemoveRange(role.Permissions);
        role.Permissions.Clear();

        foreach (var key in allowed) {
            role.Permissions.Add(new RolePermission { RoleId = role.Id, PermissionKey = key });
        }

        await _db.SaveChangesAsync(ct);
        return allowed;
    }

    public async Task<object> UpdateAsync(Ulid roleId, string name, string? description, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(name)) {
            throw new ArgumentException("Name is required", nameof(name));
        }

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Id == roleId, ct);
        if (role == null) {
            throw new InvalidOperationException("Role not found");
        }

        name = name.Trim();
        if (role.IsSystem && !string.Equals(role.Name, name, StringComparison.Ordinal)) {
            throw new InvalidOperationException("System roles cannot be renamed");
        }

        var exists = await _db.Roles.AnyAsync(r => r.Name == name && r.Id != roleId, ct);
        if (exists) {
            throw new InvalidOperationException("Role already exists");
        }

        role.Name = name;
        role.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();

        await _db.SaveChangesAsync(ct);
        return new { id = role.Id.ToString(), role.Name, role.Description };
    }

    public async Task DeleteAsync(Ulid roleId, CancellationToken ct) {
        var role = await _db.Roles
            .Include(r => r.Members)
            .Include(r => r.Permissions)
            .Include(r => r.ScreenAssignments)
            .FirstOrDefaultAsync(r => r.Id == roleId, ct);
        if (role == null) {
            throw new InvalidOperationException("Role not found");
        }

        if (role.IsSystem) {
            throw new InvalidOperationException("System roles cannot be deleted");
        }

        _db.UserRoles.RemoveRange(role.Members);
        _db.RolePermissions.RemoveRange(role.Permissions);
        _db.ScreenRoles.RemoveRange(role.ScreenAssignments);
        _db.Roles.Remove(role);

        await _db.SaveChangesAsync(ct);
    }
}
