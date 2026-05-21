using System.ComponentModel.DataAnnotations;

namespace AuthApi.Models;

/// <summary>
/// Track 3 — Portal SSO single-use nonce. Portal'da env + tenant seçilince yaratılır,
/// hedef UI callback'i `/auth/portal/handoff/consume`'da Tüketir.
/// 60 sn TTL, single-use, replay imkansız.
/// </summary>
public class PortalHandoff {
    [Key]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [Required, MaxLength(64)]
    public string Nonce { get; set; } = string.Empty;  // base64url 32 byte

    [MaxLength(26)]
    public Ulid CredentialId { get; set; }
    public Credential? Credential { get; set; }

    [MaxLength(64)]
    public string TargetUiApp { get; set; } = string.Empty;  // "console-admin"
    [MaxLength(16)]
    public string Environment { get; set; } = string.Empty;  // "dev" | "staging" | "prod"
    [MaxLength(120)]
    public string? TenantSlug { get; set; }
    [MaxLength(512)]
    public string TargetUrl { get; set; } = string.Empty;  // server-built, audit için saklı

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    [MaxLength(64)]
    public string? ConsumedFromIp { get; set; }
}
