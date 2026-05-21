using System.ComponentModel.DataAnnotations;

namespace AuthApi.Models;

public class Credential {
    [Key]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [MaxLength(120)]
    public string Login { get; set; } = string.Empty;

    [MaxLength(160)]
    public string? Email { get; set; }

    [MaxLength(256)]
    public string PasswordHash { get; set; } = string.Empty;

    public CredentialOwnerType OwnerType { get; set; } = CredentialOwnerType.TenantUser;

    [MaxLength(26)]
    public Ulid? PlatformUserId { get; set; }
    public PlatformUser? PlatformUser { get; set; }

    [MaxLength(26)]
    public Ulid? TenantUserId { get; set; }
    public TenantUser? TenantUser { get; set; }

    [MaxLength(26)]
    public Ulid? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsPrimary { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAtUtc { get; set; }
    public DateTime? AgreementsAcceptedAtUtc { get; set; }

    [MaxLength(32)]
    public string? AgreementsVersion { get; set; }

    // Security: seeded/bootstrap credentials must be rotated on first login.
    public bool MustChangePassword { get; set; } = false;
    public DateTime? PasswordChangedAtUtc { get; set; }
}
