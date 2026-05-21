using System.ComponentModel.DataAnnotations;

namespace AuthApi.Models;

public class UserRole {
    [Key]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    public Ulid RoleId { get; set; }
    public Role Role { get; set; } = null!;

    [MaxLength(26)]
    public Ulid? PlatformUserId { get; set; }
    public PlatformUser? PlatformUser { get; set; }

    [MaxLength(26)]
    public Ulid? TenantUserId { get; set; }
    public TenantUser? TenantUser { get; set; }

    [MaxLength(50)]
    public string? BranchCode { get; set; }

    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
}
