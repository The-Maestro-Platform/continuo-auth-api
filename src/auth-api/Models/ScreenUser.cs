namespace AuthApi.Models;

public class ScreenUser {
    public Ulid Id { get; set; } = Ulid.NewUlid();
    public Ulid ScreenId { get; set; }
    public Ulid PlatformUserId { get; set; }
    public Ulid? TenantId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAtUtc { get; set; }
    public string? CreatedBy { get; set; }

    public Screen? Screen { get; set; }
    public PlatformUser? PlatformUser { get; set; }
}
