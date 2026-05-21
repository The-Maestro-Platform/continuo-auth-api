using AuthApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Continuo.Observability.Attributes;

namespace AuthApi.Controllers;

/// <summary>
/// Anonymous endpoint — login sayfası (henüz auth cookie yokken) Google OAuth
/// client_id'sini bu endpoint'ten çeker. Tek doğruluk kaynağı: security-api
/// (<c>Google_ClientId</c> secret), <see cref="IGoogleAuthService"/> üzerinden
/// 5dk cache'li çözülür.
///
/// 2026-05-20: Önceden BFF <c>createGoogleAuthConfigHandler</c> parameter-api'den
/// okuyordu — Client ID iki yere (security-api + parameter-api) yazılması drift
/// üretiyordu. Tek backend endpoint'le konsolide edildi.
/// </summary>
[ApiController]
[Route("auth/google-config")]
public class GoogleAuthConfigController : ControllerBase {
    private readonly IGoogleAuthService _googleAuth;
    private readonly IConfiguration _config;

    public GoogleAuthConfigController(IGoogleAuthService googleAuth, IConfiguration config) {
        _googleAuth = googleAuth;
        _config = config;
    }

    [AllowAnonymous]
    [HttpGet]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> GetConfig(CancellationToken ct) {
        var clientId = await _googleAuth.GetClientIdAsync(ct);
        var enabled = _config.GetValue("Integrations:Auth:GoogleLoginEnabled", true);
        // Client ID yoksa login button'unu render etmemesi için enabled=false döndür —
        // frontend useGoogleAuthConfig içinde Boolean(googleClientId) zaten kontrol ediyor,
        // defansif olarak burada da false yapıyoruz.
        return Ok(new {
            googleClientId = clientId ?? string.Empty,
            googleLoginEnabled = enabled && !string.IsNullOrWhiteSpace(clientId)
        });
    }
}
