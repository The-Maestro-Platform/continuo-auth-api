using AuthApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Services;

public class RoleService {
    private readonly AuthDbContext _context;

    public RoleService(AuthDbContext context) {
        _context = context;
    }

    /// <summary>
    /// Get all permissions for a role, including inherited permissions from parent roles
    /// </summary>
    public async Task<HashSet<string>> GetEffectivePermissionsAsync(Ulid roleId) {
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await CollectPermissionsRecursivelyAsync(roleId, permissions);
        return permissions;
    }

    /// <summary>
    /// Get all permissions for multiple roles with hierarchy support
    /// </summary>
    public async Task<HashSet<string>> GetEffectivePermissionsAsync(IEnumerable<Ulid> roleIds) {
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var roleId in roleIds) {
            await CollectPermissionsRecursivelyAsync(roleId, permissions);
        }
        return permissions;
    }

    /// <summary>
    /// Recursively collect permissions from a role and its parent hierarchy
    /// </summary>
    private async Task CollectPermissionsRecursivelyAsync(Ulid roleId, HashSet<string> permissions) {
        var role = await _context.Roles
            .Include(r => r.Permissions)
            .ThenInclude(rp => rp.Permission)
            .Include(r => r.ParentRole)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        if (role == null) {
            return;
        }

        // Add direct permissions
        foreach (var rolePermission in role.Permissions) {
            permissions.Add(rolePermission.PermissionKey);
        }

        // Recursively add parent permissions
        if (role.ParentRoleId.HasValue) {
            await CollectPermissionsRecursivelyAsync(role.ParentRoleId.Value, permissions);
        }
    }

    /// <summary>
    /// Get role hierarchy tree for a given role
    /// </summary>
    public async Task<List<Role>> GetRoleHierarchyAsync(Ulid roleId) {
        var hierarchy = new List<Role>();
        var role = await _context.Roles
            .Include(r => r.ParentRole)
            .FirstOrDefaultAsync(r => r.Id == roleId);

        while (role != null) {
            hierarchy.Add(role);
            if (role.ParentRoleId.HasValue) {
                role = await _context.Roles
                    .Include(r => r.ParentRole)
                    .FirstOrDefaultAsync(r => r.Id == role.ParentRoleId.Value);
            }
            else {
                break;
            }
        }

        return hierarchy;
    }

    /// <summary>
    /// Validate that setting a parent role doesn't create a circular dependency
    /// </summary>
    public async Task<bool> ValidateParentRoleAsync(Ulid roleId, Ulid? parentRoleId) {
        if (!parentRoleId.HasValue) {
            return true;
        }

        // Check if the parent is the role itself
        if (parentRoleId.Value == roleId) {
            return false;
        }

        // Check if the parent role already has this role as an ancestor
        var parentHierarchy = await GetRoleHierarchyAsync(parentRoleId.Value);
        return !parentHierarchy.Any(r => r.Id == roleId);
    }

    /// <summary>
    /// Get all descendant roles (children, grandchildren, etc.)
    /// </summary>
    public async Task<List<Role>> GetDescendantRolesAsync(Ulid roleId) {
        var descendants = new List<Role>();
        await CollectDescendantsRecursivelyAsync(roleId, descendants);
        return descendants;
    }

    private async Task CollectDescendantsRecursivelyAsync(Ulid roleId, List<Role> descendants) {
        var children = await _context.Roles
            .Where(r => r.ParentRoleId == roleId)
            .ToListAsync();

        foreach (var child in children) {
            descendants.Add(child);
            await CollectDescendantsRecursivelyAsync(child.Id, descendants);
        }
    }
}
