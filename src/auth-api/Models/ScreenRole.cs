using System.ComponentModel.DataAnnotations;

namespace AuthApi.Models;

public class ScreenRole {
    [Key]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [MaxLength(26)]
    public Ulid ScreenId { get; set; }
    public Screen Screen { get; set; } = null!;

    [MaxLength(26)]
    public Ulid RoleId { get; set; }
    public Role Role { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
