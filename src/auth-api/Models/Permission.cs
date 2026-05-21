using System.ComponentModel.DataAnnotations;

namespace AuthApi.Models;

public class Permission {
    [Key]
    [MaxLength(120)]
    public string Key { get; set; } = string.Empty;

    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(400)]
    public string? Description { get; set; }

    public RoleScope Scope { get; set; } = RoleScope.Tenant;

    // UI Metadata for dynamic frontend rendering
    [MaxLength(80)]
    public string? Category { get; set; }

    [MaxLength(80)]
    public string? Icon { get; set; }

    public int SortOrder { get; set; }

    public ICollection<RolePermission> Roles { get; set; } = new List<RolePermission>();
}
