using System.Text.Json;
using Google.Apis.Auth;
using Continuo.Configuration.Extensions;

namespace AuthApi.Services;

public record GoogleUserInfo(
    string Sub,
    string Email,
    bool EmailVerified,
    string? Name,
    string? Picture
);

public interface IGoogleAuthService {
    Task<GoogleUserInfo?> ValidateIdTokenAsync(string idToken, CancellationToken ct = default);
    Task<string?> GetClientIdAsync(CancellationToken ct = default);
}

/// <summary>
/// Google OAuth ID-token doğrulayıcısı.
///
/// 2026-05-20: Client ID artık <see cref="IPlatformSecretResolver"/> üzerinden
/// security-api'nin <c>/security/runtime/secrets/Google_ClientId</c> endpoint'inden
/// çekiliyor (5dk in-memory cache). ENV (<c>GOOGLE:CLIENTID</c>) yalnızca dev-bootstrap
/// fallback'i olarak korunuyor — tek doğruluk kaynağı security-api.
///
/// NOT: Google ID-Token flow Client Secret kullanmaz. `id_token` Google'ın
/// public key'leri ile imzalanmıştır; sadece <c>aud</c> claim'ini kendi Client ID'mizle
/// kıyaslıyoruz. Authorization Code flow (server-side OAuth) kullanılsaydı
/// `Google_ClientSecret` da secret-resolver'a yazılır + buradan okunur idi.
/// </summary>
public class GoogleAuthService : IGoogleAuthService {
    // Primary name (önerim, snake_case). Operatör hatalarına dayanıklı olmak için
    // legacy ve env-style isimlerine de fallback yapıyoruz (SecurityDefinitionPanel'in
    // önerdiği eski "GOOGLE__CLIENTSECRET" — yanlış semantikti ama veri Client ID'idi).
    // İlk dolu olan kazanır.
    private static readonly string[] ClientIdSecretNames = new[] {
        "Google_ClientId",
        "GOOGLE__CLIENTID",
        "GOOGLE_CLIENT_ID",
        "GOOGLE__CLIENTSECRET" // legacy mis-name: panel eski sürümünde bu isim önerilmişti
    };

    private readonly IConfiguration _config;
    private readonly IPlatformSecretResolver _secretResolver;
    private readonly ILogger<GoogleAuthService> _logger;

    public GoogleAuthService(
        IConfiguration config,
        IPlatformSecretResolver secretResolver,
        ILogger<GoogleAuthService> logger) {
        _config = config;
        _secretResolver = secretResolver;
        _logger = logger;
    }

    public async Task<string?> GetClientIdAsync(CancellationToken ct = default) {
        // 1. Security-api authoritative kaynak — birden fazla isim convention'ında dener
        //    (resilient against operator typos / legacy panel önerileri).
        foreach (var name in ClientIdSecretNames) {
            try {
                var fromSecret = await _secretResolver.TryResolveAsync(name, ct);
                if (!string.IsNullOrWhiteSpace(fromSecret)) {
                    if (name != ClientIdSecretNames[0]) {
                        _logger.LogWarning(
                            "Google Client ID '{LegacyName}' adından çözüldü. Operasyonel netlik için " +
                            "'{PrimaryName}' adına migrate edin.", name, ClientIdSecretNames[0]);
                    }
                    return fromSecret.Trim();
                }
            }
            catch (Exception ex) {
                _logger.LogDebug(ex, "PlatformSecretResolver '{Name}' okuma hatası — sonraki ada geçilecek.", name);
            }
        }

        // 2. ENV / config fallback — dev bootstrap için.
        var fromEnv = _config["GOOGLE:CLIENTID"]
                      ?? _config["GOOGLE__CLIENTID"]
                      ?? Environment.GetEnvironmentVariable("GOOGLE__CLIENTID");
        if (!string.IsNullOrWhiteSpace(fromEnv)) {
            _logger.LogInformation(
                "Google Client ID ENV'den çözüldü. Production'da security-api'ye 'Google_ClientId' yaz.");
            return fromEnv.Trim();
        }

        return null;
    }

    public async Task<GoogleUserInfo?> ValidateIdTokenAsync(string idToken, CancellationToken ct = default) {
        var clientId = await GetClientIdAsync(ct);
        if (string.IsNullOrEmpty(clientId)) {
            _logger.LogError(
                "Google Client ID yapılandırılmamış. security-api'ye '{SecretName}' secret'i yaz (kind=generic) " +
                "veya GOOGLE__CLIENTID env değişkenini set et.",
                ClientIdSecretNames[0]);
            return null;
        }

        try {
            var settings = new GoogleJsonWebSignature.ValidationSettings {
                Audience = new[] { clientId }
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

            return new GoogleUserInfo(
                Sub: payload.Subject,
                Email: payload.Email,
                EmailVerified: payload.EmailVerified,
                Name: payload.Name,
                Picture: payload.Picture
            );
        } catch (InvalidJwtException ex) {
            _logger.LogWarning(ex, "Invalid Google ID token");
            return null;
        } catch (Exception ex) {
            _logger.LogError(ex, "Error validating Google ID token");
            return null;
        }
    }
}
