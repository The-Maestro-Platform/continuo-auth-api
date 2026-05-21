using System.ComponentModel.DataAnnotations;

namespace AuthApi.Models;

public class SystemLog {
    [Key]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [MaxLength(26)]
    public Ulid? UserId { get; set; }

    public UserType UserType { get; set; }

    [MaxLength(26)]
    public Ulid? TenantId { get; set; }

    [MaxLength(160)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(160)]
    public string? EntityType { get; set; }

    [MaxLength(160)]
    public string? EntityId { get; set; }

    public string? Metadata { get; set; }
}
