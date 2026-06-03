using System.Globalization;
using System.Text.RegularExpressions;
using AuthApi.Models;

namespace AuthApi.Services;

/// <summary>
/// Resolves <c>{{token}}</c> placeholders inside agreement bodies against the
/// current <see cref="PlatformIdentity"/> and per-agreement metadata. Used by
/// the public <c>GET /auth/platform-agreements/active</c> endpoint and by the
/// tc-ops-ui editor preview (which calls a separate render endpoint).
/// <para>
/// Mustache-light: only <c>{{ name }}</c> is recognised, no logic, no
/// partials. Unknown tokens are left as-is so operators can quickly notice
/// typos in the published metin.
/// </para>
/// </summary>
public static class PlatformIdentityRenderer {
    private static readonly Regex TokenRegex = new(@"\{\{\s*([a-zA-Z][a-zA-Z0-9_.]*)\s*\}\}", RegexOptions.Compiled);

    /// <summary>Resolve tokens in <paramref name="body"/> using the identity
    /// fields and the supplied agreement-level metadata. Returns the body
    /// unchanged if no tokens are present.</summary>
    public static string Render(string? body, PlatformIdentity identity, AgreementRenderMetadata? meta = null) {
        if (string.IsNullOrEmpty(body)) return body ?? string.Empty;
        if (!body.Contains("{{", StringComparison.Ordinal)) return body;

        var values = BuildTokenMap(identity, meta);
        return TokenRegex.Replace(body, match => {
            var key = match.Groups[1].Value;
            return values.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v)
                ? v
                : match.Value; // leave unknown tokens visible
        });
    }

    /// <summary>The canonical token catalog — exposed so tc-ops-ui can list
    /// the available tokens to operators inside the editor cheat-sheet.</summary>
    public static IReadOnlyList<TokenDescriptor> TokenCatalog { get; } = new[] {
        new TokenDescriptor("companyName",      "Şirket Ticari Adı",  "Kısa ticari ad, örn. \"Continuo\""),
        new TokenDescriptor("companyLegalName", "Tüzel Kişilik",      "Tam unvan, örn. \"Continuo Bilişim Hiz. A.Ş.\""),
        new TokenDescriptor("companyAddress",   "Açık Adres",         "Yasal merkez adresi"),
        new TokenDescriptor("companyEmail",     "İletişim E-postası", "destek@... gibi"),
        new TokenDescriptor("companyKep",       "KEP Adresi",         "KVKK başvurusu için zorunlu"),
        new TokenDescriptor("companyPhone",     "Telefon",            "Müşteri hizmetleri"),
        new TokenDescriptor("companyWebsite",   "Web Adresi",         "example.local"),
        new TokenDescriptor("jurisdictionCity", "Yetkili Mahkeme",    "Uyuşmazlıkta yetkili şehir (ör. İstanbul)"),
        new TokenDescriptor("agreementTitle",   "Sözleşme Başlığı",   "Aktif sözleşmenin başlığı"),
        new TokenDescriptor("agreementVersion", "Versiyon Etiketi",   "Aktif sözleşmenin versiyon kodu"),
        new TokenDescriptor("agreementDate",    "Yürürlük Tarihi",    "Sözleşmenin yürürlük tarihi (TR)"),
    };

    private static Dictionary<string, string> BuildTokenMap(PlatformIdentity i, AgreementRenderMetadata? meta) {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            ["companyName"]      = i.CompanyName,
            ["companyLegalName"] = i.CompanyLegalName,
            ["companyAddress"]   = i.CompanyAddress,
            ["companyEmail"]     = i.CompanyEmail,
            ["companyKep"]       = i.CompanyKep,
            ["companyPhone"]     = i.CompanyPhone,
            ["companyWebsite"]   = i.CompanyWebsite,
            ["jurisdictionCity"] = i.JurisdictionCity,
        };
        if (meta is not null) {
            map["agreementTitle"]   = meta.Title;
            map["agreementVersion"] = meta.Version;
            map["agreementDate"]    = meta.EffectiveFromUtc.ToString("d MMMM yyyy", new CultureInfo("tr-TR"));
        }
        return map;
    }

    public record TokenDescriptor(string Key, string Label, string Description);
}

public record AgreementRenderMetadata(string Title, string Version, DateTime EffectiveFromUtc);
