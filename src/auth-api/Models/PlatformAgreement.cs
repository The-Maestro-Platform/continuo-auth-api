namespace AuthApi.Models;

/// <summary>
/// Platform-level legal agreement displayed to customers at signup/login.
/// One active row per <see cref="Code"/>; historical versions are kept with
/// <see cref="IsActive"/>=false for audit. Bodies are markdown so the
/// continuo-ops-ui editor can render preview + copy unmodified to mobile/web.
/// </summary>
public class PlatformAgreement {
    public Ulid Id { get; set; }

    /// <summary>Business key: <c>terms</c>, <c>kvkk</c>, <c>marketing</c>.</summary>
    public string Code { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>Markdown body, rendered by qrmenu-mobile + qrmenu-web.</summary>
    public string BodyMd { get; set; } = string.Empty;

    /// <summary>Free-form revision tag (e.g. <c>2026-05-29</c>); customer
    /// credential's AgreementsVersion records which revision they accepted.</summary>
    public string Version { get; set; } = string.Empty;

    public DateTime EffectiveFromUtc { get; set; }

    public bool IsActive { get; set; }

    /// <summary>Required = blocks signup if unchecked (terms, kvkk).
    /// Optional = marketing-style opt-in.</summary>
    public bool IsRequired { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}
