namespace AuthApi.Controllers;

/// <summary>
/// App-context routing constants for login authorization.
/// Phase-1 move-only extraction from <see cref="AuthController"/>.
/// </summary>
internal static class AuthConstants {
    /// <summary>
    /// App-aware platform UI listesi — bu UI'lara sadece PlatformUser girer
    /// (ortam farketmez). Tenant admin app'leri (console-admin) ve customer-
    /// facing app'ler ayri kurallarla degerlendirilir.
    /// </summary>
    public static readonly HashSet<string> PlatformOnlyApps = new(StringComparer.OrdinalIgnoreCase) {
        "continuo-ops-ui",
        "dev-support-console",
    };

    public static readonly HashSet<string> CustomerFacingApps = new(StringComparer.OrdinalIgnoreCase) {
        "qrmenu-web",
        "qrmenu-mobile",
        "public-web",
        "continuo-web"
    };
}
