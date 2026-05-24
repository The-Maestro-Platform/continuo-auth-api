namespace AuthApi.Models.PlatformSettings;

/// <summary>
/// Runtime-editable platform branding/identity surface. Backed by the generic
/// Parameters store at <c>module=platform, section=branding</c>. Consumers
/// (continuo-maestro-api, maestro-console UI) call <c>GET /auth/platform-
/// settings</c> with optional tenant scope; tenant values overlay platform
/// defaults for the 5 overrideable fields (brand/assistant/domainHint/
/// theme/logo). GithubRepo + UserAgent are platform-only.
///
/// All fields nullable so PUT can apply partial updates without overwriting
/// other keys. See <c>docs/PLATFORM_SETTINGS_PLAN.md</c> in maestro-console.
/// </summary>
public sealed record PlatformBrandingSettingsDto(
    string? BrandName,
    string? AssistantName,
    string? DomainHint,
    string? GithubRepo,
    string? UserAgent,
    string? ThemeAccentColor,
    string? LogoUrl) {

    public const string Module = "platform";
    public const string Section = "branding";

    public static readonly string[] AllKeys = [
        Keys.BrandName,
        Keys.AssistantName,
        Keys.DomainHint,
        Keys.GithubRepo,
        Keys.UserAgent,
        Keys.ThemeAccentColor,
        Keys.LogoUrl,
    ];

    /// <summary>Subset that may be overridden at tenant scope.</summary>
    public static readonly string[] TenantOverrideableKeys = [
        Keys.BrandName,
        Keys.AssistantName,
        Keys.DomainHint,
        Keys.ThemeAccentColor,
        Keys.LogoUrl,
    ];

    public static class Keys {
        public const string BrandName = "brandName";
        public const string AssistantName = "assistantName";
        public const string DomainHint = "domainHint";
        public const string GithubRepo = "githubRepo";
        public const string UserAgent = "userAgent";
        public const string ThemeAccentColor = "themeAccentColor";
        public const string LogoUrl = "logoUrl";
    }

    public static class Defaults {
        public const string BrandName = "Continuo";
        public const string AssistantName = "Maestro";
        public const string DomainHint = "multi-tenant platform";
        public const string GithubRepo = "";
        public const string UserAgent = "Continuo-Maestro/1.0";
        public const string ThemeAccentColor = "#a855f7";
        public const string LogoUrl = "";
    }

    public static PlatformBrandingSettingsDto DefaultsDto() => new(
        Defaults.BrandName,
        Defaults.AssistantName,
        Defaults.DomainHint,
        Defaults.GithubRepo,
        Defaults.UserAgent,
        Defaults.ThemeAccentColor,
        Defaults.LogoUrl);
}
