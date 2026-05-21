using System.ComponentModel.DataAnnotations;

namespace AuthApi.Models;

public class TwoFactorChallenge {
    [Key]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [MaxLength(26)]
    public Ulid CredentialId { get; set; }
    public Credential Credential { get; set; } = default!;

    [MaxLength(160)]
    public string Channel { get; set; } = "Email";

    [MaxLength(256)]
    public string Target { get; set; } = string.Empty;

    [MaxLength(128)]
    public string CodeHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddMinutes(10);
    public DateTime? VerifiedAtUtc { get; set; }

    public int FailedAttempts { get; set; }

    [MaxLength(200)]
    public string? LastError { get; set; }

    public DateTime? LastResendAtUtc { get; set; }

    public bool IsExpired(DateTime nowUtc) => nowUtc >= ExpiresAtUtc;
    public bool IsVerified => VerifiedAtUtc.HasValue;
}
