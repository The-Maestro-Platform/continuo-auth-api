using System.ComponentModel.DataAnnotations;

namespace AuthApi.Models;

public class ExternalLogin {
    [Key]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    public Ulid CredentialId { get; set; }
    public Credential? Credential { get; set; }

    [MaxLength(50)]
    public string Provider { get; set; } = string.Empty;

    [MaxLength(256)]
    public string ProviderUserId { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? ProviderEmail { get; set; }

    [MaxLength(256)]
    public string? ProviderDisplayName { get; set; }

    [MaxLength(512)]
    public string? ProfilePictureUrl { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAtUtc { get; set; }
}
