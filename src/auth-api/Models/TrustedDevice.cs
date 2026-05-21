using System.ComponentModel.DataAnnotations;

namespace AuthApi.Models;

public class TrustedDevice {
    [Key]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [MaxLength(26)]
    public Ulid CredentialId { get; set; }
    public Credential Credential { get; set; } = default!;

    /// <summary>
    /// SHA-256 hash of the opaque per-device secret. The raw secret is only
    /// ever held by the browser (HttpOnly cookie via BFF) — server stores the
    /// hash so a DB leak does not let an attacker forge trust cookies.
    /// </summary>
    [MaxLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    [MaxLength(512)]
    public string? UserAgent { get; set; }

    [MaxLength(64)]
    public string? IpAddress { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime LastUsedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAtUtc { get; set; }

    public bool IsActive(DateTime nowUtc) => RevokedAtUtc == null && nowUtc < ExpiresAtUtc;
}
