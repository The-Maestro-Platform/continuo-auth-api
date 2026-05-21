using System.ComponentModel.DataAnnotations;

namespace AuthApi.Models;

public class Role {
    [Key]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Description { get; set; }

    public RoleScope Scope { get; set; } = RoleScope.Tenant;

    public bool IsSystem { get; set; }

    // Role Hierarchy Support
    public Ulid? ParentRoleId { get; set; }
    public Role? ParentRole { get; set; }
    public ICollection<Role> ChildRoles { get; set; } = new List<Role>();

    public ICollection<RolePermission> Permissions { get; set; } = new List<RolePermission>();
    public ICollection<UserRole> Members { get; set; } = new List<UserRole>();
    public ICollection<ScreenRole> ScreenAssignments { get; set; } = new List<ScreenRole>();
}
