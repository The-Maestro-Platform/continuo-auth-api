namespace AuthApi.Models;

/// <summary>
/// Single-row platform identity (legal company info) used to resolve
/// <c>{{companyName}}</c> / <c>{{companyEmail}}</c> / <c>{{companyKep}}</c>
/// style tokens inside <see cref="PlatformAgreement.BodyMd"/> bodies before
/// they are served to qrmenu-mobile / qrmenu-web.
/// <para>
/// The row is a singleton — PK is fixed to <c>RowKey = "current"</c> so
/// `INSERT IF NOT EXISTS` + idempotent update queries stay simple. Multiple
/// rows would defeat the "one company per platform" mental model.
/// </para>
/// </summary>
public class PlatformIdentity {
    /// <summary>Single-row marker. Always equals <c>"current"</c>.</summary>
    public string RowKey { get; set; } = "current";

    public string CompanyName { get; set; } = string.Empty;
    public string CompanyLegalName { get; set; } = string.Empty;
    public string CompanyAddress { get; set; } = string.Empty;
    public string CompanyEmail { get; set; } = string.Empty;
    public string CompanyKep { get; set; } = string.Empty;
    public string CompanyPhone { get; set; } = string.Empty;
    public string CompanyWebsite { get; set; } = string.Empty;

    /// <summary>Yetkili mahkeme şehri — KVKK ve Kullanım Koşulları m.9'da kullanılır.</summary>
    public string JurisdictionCity { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}
