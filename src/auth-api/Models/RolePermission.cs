using System.ComponentModel.DataAnnotations;

namespace AuthApi.Models;

public class RolePermission {
    public Ulid RoleId { get; set; }
    public Role Role { get; set; } = null!;

    [MaxLength(120)]
    public string PermissionKey { get; set; } = string.Empty;

    public Permission Permission { get; set; } = null!;
}
