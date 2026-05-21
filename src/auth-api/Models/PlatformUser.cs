using System.ComponentModel.DataAnnotations;

namespace AuthApi.Models;

public class PlatformUser {
    [Key]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [MaxLength(160)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(160)]
    public string DisplayName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<Credential> Credentials { get; set; } = new List<Credential>();
    public ICollection<UserRole> Roles { get; set; } = new List<UserRole>();
}
