using System.ComponentModel.DataAnnotations;

namespace AuthApi.Models;

public class PasswordResetToken {
    [Key]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [MaxLength(26)]
    public Ulid CredentialId { get; set; }
    public Credential Credential { get; set; } = null!;

    [MaxLength(64)]
    public string AppId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }

    public int AttemptCount { get; set; }

    [MaxLength(64)]
    public string? RequestIp { get; set; }

    [MaxLength(512)]
    public string? UserAgent { get; set; }

    public bool IsActive(DateTime nowUtc) => ConsumedAtUtc == null && nowUtc < ExpiresAtUtc;
}
