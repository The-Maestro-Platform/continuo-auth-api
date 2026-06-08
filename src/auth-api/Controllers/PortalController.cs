using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using AuthApi.Models;
using AuthApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Continuo.Observability.Attributes;

namespace AuthApi.Controllers;

/// <summary>
/// Track 3 — Portal SSO handoff. Portal'da login olan platform user
/// env + tenant seçer, Issue endpoint server-built URL + tek kullanımlık nonce
/// yaratır. Hedef UI callback'inde Consume nonce'u tüketir, fresh JWT döner.
/// </summary>
[ApiController]
[Route("auth/portal")]
public class PortalController : ControllerBase {
    private static readonly HashSet<string> AllowedUis = new(StringComparer.OrdinalIgnoreCase) {
        "console-admin", "continuo-ops-ui", "dev-support-console", "qrmenu-web", "continuo-web"
    };

    private static readonly HashSet<string> AllowedEnvs = new(StringComparer.OrdinalIgnoreCase) {
        "dev", "staging", "prod"
    };

    private readonly AuthDbContext _db;
    private readonly ITokenService _tokens;
    private readonly IConfiguration _config;
    private readonly IScreenAccessService _screens;
    private readonly ISessionService _sessions;

    public PortalController(
        AuthDbContext db,
        ITokenService tokens,
        IConfiguration config,
        IScreenAccessService screens,
        ISessionService sessions) {
        _db = db;
        _tokens = tokens;
        _config = config;
        _screens = screens;
        _sessions = sessions;
    }

    private string ResolveOwnerLogin() =>
        (_config["AUTH:OWNER_LOGIN"]
         ?? _config["AUTH__OWNER_LOGIN"]
         ?? Environment.GetEnvironmentVariable("AUTH_OWNER_LOGIN")
         ?? "platform.owner@example.local").Trim();

    private bool IsPlatformOwner(Credential credential) {
        var ownerLogin = ResolveOwnerLogin();
        return string.Equals(credential.Login?.Trim(), ownerLogin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(credential.Email?.Trim(), ownerLogin, StringComparison.OrdinalIgnoreCase);
    }

    private string? ResolveClientIp() {
        var fwd = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(fwd)) {
            return fwd.Split(',')[0].Trim();
        }
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private string? ResolveUserAgent() =>
        HttpContext.Request.Headers["User-Agent"].FirstOrDefault();

    public record IssueRequest(string TargetUiApp, string Environment, string? TenantSlug);
    public record ConsumeRequest(string Nonce);

    [ContinuoProxyMethod("ui")]
    [HttpPost("handoff/issue")]
    [Authorize]
    public async Task<IActionResult> Issue([FromBody] IssueRequest req, CancellationToken ct) {
        if (req is null
            || string.IsNullOrWhiteSpace(req.TargetUiApp)
            || string.IsNullOrWhiteSpace(req.Environment)) {
            return BadRequest(new { message = "TargetUiApp ve Environment zorunlu" });
        }

        var env = req.Environment.Trim().ToLowerInvariant();
        var ui = req.TargetUiApp.Trim().ToLowerInvariant();
        if (!AllowedEnvs.Contains(env)) {
            return BadRequest(new { message = "Geçersiz environment" });
        }
        if (!AllowedUis.Contains(ui)) {
            return BadRequest(new { message = "Geçersiz UI app" });
        }

        // Permission gating
        var permClaim = User.Claims
            .Where(c => c.Type == "permissions" || c.Type == "perm")
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!permClaim.Contains(Permissions.PermissionKeys.Platform.PortalAccess)) {
            return Forbid();
        }
        var envPermKey = env switch {
            "dev" => Permissions.PermissionKeys.Platform.PortalEnvDev,
            "staging" => Permissions.PermissionKeys.Platform.PortalEnvStaging,
            "prod" => Permissions.PermissionKeys.Platform.PortalEnvProd,
            _ => null
        };
        if (envPermKey is not null && !permClaim.Contains(envPermKey)) {
            return Forbid();
        }

        var credentialIdStr = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                              ?? User.FindFirst("sub")?.Value
                              ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(credentialIdStr) || !Ulid.TryParse(credentialIdStr, out var credentialId)) {
            return Unauthorized();
        }

        var domain = _config["PORTAL__DOMAIN"] ?? _config["PORTAL:DOMAIN"] ?? "example.local";
        // URL pattern: https://{env}.{ui}.{domain}/auth/portal-callback?nonce=...&tenant=...
        // Server builds — client'tan asla URL alınmaz (open-redirect koruması).
        var nonce = GenerateNonce();
        var tenantParam = string.IsNullOrWhiteSpace(req.TenantSlug)
            ? ""
            : $"&tenant={Uri.EscapeDataString(req.TenantSlug.Trim())}";
        var url = $"https://{env}.{ui}.{domain}/auth/portal-callback?nonce={Uri.EscapeDataString(nonce)}{tenantParam}";

        var handoff = new PortalHandoff {
            Nonce = nonce,
            CredentialId = credentialId,
            TargetUiApp = ui,
            Environment = env,
            TenantSlug = req.TenantSlug?.Trim(),
            TargetUrl = url,
            ExpiresAtUtc = DateTime.UtcNow.AddSeconds(60)
        };
        _db.PortalHandoffs.Add(handoff);
        await _db.SaveChangesAsync(ct);

        return Ok(new { url, expiresAtUtc = handoff.ExpiresAtUtc });
    }

    [ContinuoProxyMethod("ui")]
    [HttpPost("handoff/consume")]
    [AllowAnonymous]   // nonce kanıt yerine geçer
    public async Task<IActionResult> Consume([FromBody] ConsumeRequest req, CancellationToken ct) {
        if (req is null || string.IsNullOrWhiteSpace(req.Nonce)) {
            return BadRequest(new { message = "Nonce zorunlu" });
        }

        var handoff = await _db.PortalHandoffs
            .Include(h => h.Credential)
                .ThenInclude(c => c!.PlatformUser)
                    .ThenInclude(u => u!.Roles)
                        .ThenInclude(r => r.Role!)
                            .ThenInclude(role => role!.Permissions)
            .FirstOrDefaultAsync(h => h.Nonce == req.Nonce, ct);

        if (handoff is null) {
            return BadRequest(new { message = "invalid" });
        }
        if (handoff.ConsumedAtUtc is not null) {
            return BadRequest(new { message = "consumed" });
        }
        if (handoff.ExpiresAtUtc < DateTime.UtcNow) {
            return BadRequest(new { message = "expired" });
        }

        handoff.ConsumedAtUtc = DateTime.UtcNow;
        handoff.ConsumedFromIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        await _db.SaveChangesAsync(ct);

        var credential = handoff.Credential ?? await _db.Credentials.FirstAsync(c => c.Id == handoff.CredentialId, ct);
        var roles = (credential.PlatformUser?.Roles ?? new List<UserRole>())
            .Where(r => r.Role is not null)
            .Select(r => r.Role!)
            .Distinct()
            .ToList();
        var roleNames = roles.Select(r => r.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var permissionKeys = roles.SelectMany(r => r.Permissions.Select(p => p.PermissionKey))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var screens = await _screens.ResolveScreensAsync(credential, permissionKeys, roles, handoff.TargetUiApp, ct);

        // 2026-05-24: portal handoff now mints the same opaque-session pair as
        // /auth/login. The destination UI's route.ts writes only the opaque
        // token into the browser cookie; the BFF exchanges it back to a JWT
        // per request via /auth/session/exchange. accessToken stays in the
        // response body for callers still on the legacy path (rolling deploy).
        var (session, opaqueSessionToken) = await _sessions.CreateAsync(
            credential.Id,
            handoff.TargetUiApp,
            exemptFromSingleSession: IsPlatformOwner(credential),
            ResolveClientIp(),
            ResolveUserAgent(),
            ct);

        var (accessToken, accessExpires) = await _tokens.CreateAccessTokenAsync(
            credential, roleNames, permissionKeys, screens, branchCodes: null, branchRoles: null, sessionId: session.Id);
        var (refreshToken, refreshExpires) = await _tokens.CreateRefreshTokenAsync();

        return Ok(new {
            accessToken, accessExpires,
            sessionToken = opaqueSessionToken,
            refreshToken, refreshExpires,
            credential = new {
                id = credential.Id.ToString(),
                login = credential.Login,
                email = credential.Email,
                ownerType = credential.OwnerType,
                roles = roleNames,
                permissions = permissionKeys,
                screens
            },
            targetTenant = handoff.TenantSlug,
            targetUiApp = handoff.TargetUiApp,
            environment = handoff.Environment
        });
    }

    /// <summary>
    /// Portal sayfası için: kullanıcının erişebildiği tenant slug'larını döner.
    /// PlatformOwner rolü tüm tenant'ları görür.
    /// </summary>
    [ContinuoProxyMethod("ui")]
    [HttpGet("tenants")]
    [Authorize]
    public async Task<IActionResult> Tenants(CancellationToken ct) {
        var permClaim = User.Claims
            .Where(c => c.Type == "permissions" || c.Type == "perm")
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!permClaim.Contains(Permissions.PermissionKeys.Platform.PortalAccess)) {
            return Forbid();
        }
        var tenants = await _db.Tenants
            .Where(t => t.Status == TenantStatus.Active)
            .OrderBy(t => t.Name)
            .Select(t => new { id = t.Id.ToString(), code = t.Code, name = t.Name, slug = t.Slug })
            .ToListAsync(ct);
        return Ok(tenants);
    }

    private static string GenerateNonce() {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }
}
