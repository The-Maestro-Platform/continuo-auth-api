using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthApi.Models;
using AuthApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace AuthApi.Controllers;

/// <summary>
/// `/auth/claims` — UI BFF (AuthGate provider) bu endpoint'i çağırıp
/// roles/permissions/screens listesini DB-fresh olarak alır. Cookie boyutu
/// limitlemesi yüzünden JWT'de `screen` claim'i taşınmaz (2026-05-20 incident:
/// mert.cengiz login loop). Permission claim'i şimdilik JWT'de kalır (backend
/// permission check'leri henüz HTTP-resolver mimarisine geçmedi), ancak `/claims`
/// yine de DB'den authoritative permission seti döner — UI artık JWT'ye değil
/// bu response'a güvenir.
/// </summary>
[ApiController]
[Route("auth/claims")]
public class AuthClaimsController : ControllerBase {
    private readonly AuthDbContext _db;
    private readonly IScreenAccessService _screenAccess;
    private readonly ISessionService _sessionService;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthClaimsController> _logger;

    public AuthClaimsController(
        AuthDbContext db,
        IScreenAccessService screenAccess,
        ISessionService sessionService,
        IConfiguration config,
        ILogger<AuthClaimsController> logger) {
        _db = db;
        _screenAccess = screenAccess;
        _sessionService = sessionService;
        _config = config;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> GetClaims([FromQuery] string? app = null, CancellationToken ct = default) {
        // Token resolution order:
        //   1. X-Session-Token header (BFF native path).
        //   2. Authorization: Bearer / auth_token cookie / ?token=… (orchestrator
        //      may convert an opaque cookie into a Bearer header before forward).
        //      A 3-segment value is treated as a JWT; anything else is tried as
        //      an opaque session token so the rolling-deploy window is forgiving.
        var opaqueSessionToken = HttpContext.Request.Headers["X-Session-Token"].FirstOrDefault()?.Trim();
        var bearerOrCookie = ResolveToken();
        Ulid credentialId;

        if (string.IsNullOrWhiteSpace(opaqueSessionToken) && !string.IsNullOrWhiteSpace(bearerOrCookie) && !LooksLikeJwt(bearerOrCookie)) {
            opaqueSessionToken = bearerOrCookie;
        }

        if (!string.IsNullOrWhiteSpace(opaqueSessionToken)) {
            var session = await _sessionService.ResolveByOpaqueTokenAsync(opaqueSessionToken, ct);
            if (session == null) {
                return Unauthorized(new { message = "session_invalid" });
            }
            credentialId = session.CredentialId;
        }
        else {
            // Legacy JWT-bearer path — kept while older browsers still hold a
            // JWT cookie. After the rolling cutover this branch can be removed.
            if (string.IsNullOrWhiteSpace(bearerOrCookie)) {
                return Unauthorized(new { message = "Missing token" });
            }

            var secret = _config["JWT:SECRET"] ?? _config["JWT__SECRET"];
            if (string.IsNullOrWhiteSpace(secret)) {
                return StatusCode(500, new { message = "JWT secret missing" });
            }

            var issuer = _config["JWT:ISSUER"] ?? _config["JWT__ISSUER"];
            var audience = _config["JWT:AUDIENCE"] ?? _config["JWT__AUDIENCE"];

            var validationParams = new TokenValidationParameters {
                ValidateIssuer = !string.IsNullOrWhiteSpace(issuer),
                ValidIssuer = issuer,
                ValidateAudience = !string.IsNullOrWhiteSpace(audience),
                ValidAudience = audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            ClaimsPrincipal principal;
            try {
                var handler = new JwtSecurityTokenHandler();
                principal = handler.ValidateToken(bearerOrCookie, validationParams, out _);
            }
            // IDX12741 surfaces as ArgumentException for malformed inputs (no 3 segments),
            // and SecurityTokenException for cryptographic / lifetime failures. Treat
            // both as "not a usable JWT" — opaque was already attempted above.
            catch (Exception ex) when (ex is SecurityTokenException || ex is ArgumentException) {
                return Unauthorized(new { message = "Invalid token" });
            }

            var credentialIdRaw = principal.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.NameIdentifier ||
                c.Type == JwtRegisteredClaimNames.Sub ||
                c.Type == "sub")?.Value;
            if (string.IsNullOrWhiteSpace(credentialIdRaw) || !Ulid.TryParse(credentialIdRaw, out credentialId)) {
                return Unauthorized(new { message = "Invalid token subject" });
            }
        }

        var credential = await _db.Credentials
            .Include(c => c.PlatformUser)
                .ThenInclude(u => u!.Roles)
                    .ThenInclude(r => r.Role!)
                        .ThenInclude(role => role!.Permissions)
            .Include(c => c.TenantUser)
                .ThenInclude(u => u!.Tenant)
            .Include(c => c.TenantUser)
                .ThenInclude(u => u!.Roles)
                    .ThenInclude(r => r.Role!)
                        .ThenInclude(role => role!.Permissions)
            .Include(c => c.Customer)
                .ThenInclude(cust => cust!.Tenant)
            .FirstOrDefaultAsync(c => c.IsActive && c.Id == credentialId, ct);

        if (credential == null) {
            return Unauthorized(new { message = "Credential not found or inactive" });
        }

        var ownerRoles = ResolveOwnerRoles(credential).ToList();
        var roleNames = ownerRoles.Select(r => r.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var permissions = ownerRoles
            .SelectMany(r => r.Permissions.Select(p => p.PermissionKey))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var screens = await _screenAccess.ResolveScreensAsync(credential, permissions, ownerRoles, app, ct);

        var userRoles = ResolveUserRoles(credential).ToList();
        var branchCodes = userRoles
            .Where(ur => !string.IsNullOrWhiteSpace(ur.BranchCode))
            .Select(ur => ur.BranchCode!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var branchRoles = userRoles
            .Where(ur => !string.IsNullOrWhiteSpace(ur.BranchCode))
            .Select(ur => $"{ur.BranchCode}:{ur.Role.Name}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var displayName =
            (credential.OwnerType == CredentialOwnerType.PlatformUser ? credential.PlatformUser?.DisplayName : null)
            ?? (credential.OwnerType == CredentialOwnerType.TenantUser ? credential.TenantUser?.DisplayName : null)
            ?? (credential.OwnerType == CredentialOwnerType.Customer ? credential.Customer?.DisplayName : null)
            ?? credential.Login;

        var tenantCode =
            credential.TenantUser?.Tenant?.Code
            ?? credential.Customer?.Tenant?.Code;
        var tenantSlug =
            credential.TenantUser?.Tenant?.Slug
            ?? credential.Customer?.Tenant?.Slug
            ?? tenantCode;
        var tenantName =
            credential.TenantUser?.Tenant?.Name
            ?? credential.Customer?.Tenant?.Name;

        var ownerLogin = _config["AUTH_OWNER_LOGIN"]
                         ?? Environment.GetEnvironmentVariable("AUTH_OWNER_LOGIN")
                         ?? "platform.owner@example.local";
        var screensList = screens.ToList();
        if (!string.IsNullOrWhiteSpace(credential.Login)
            && string.Equals(credential.Login, ownerLogin, StringComparison.OrdinalIgnoreCase)
            && !screensList.Contains("*")) {
            screensList.Add("*");
        }

        return Ok(new {
            login = credential.Login,
            name = displayName,
            userType = credential.OwnerType.ToString(),
            userId = (credential.OwnerType == CredentialOwnerType.PlatformUser ? credential.PlatformUser?.Id.ToString() : null)
                     ?? (credential.OwnerType == CredentialOwnerType.TenantUser ? credential.TenantUser?.Id.ToString() : null)
                     ?? (credential.OwnerType == CredentialOwnerType.Customer ? credential.Customer?.Id.ToString() : null)
                     ?? credential.Id.ToString(),
            credentialId = credential.Id.ToString(),
            tenantCode,
            tenantSlug,
            tenantName,
            roles = roleNames,
            permissions,
            screens = screensList.ToArray(),
            branchCodes,
            branchRoles,
            app
        });
    }

    private static IEnumerable<Role> ResolveOwnerRoles(Credential credential) {
        if (credential.OwnerType == CredentialOwnerType.PlatformUser && credential.PlatformUser != null) {
            return credential.PlatformUser.Roles.Select(r => r.Role!).Where(r => r != null);
        }
        if (credential.OwnerType == CredentialOwnerType.TenantUser && credential.TenantUser != null) {
            return credential.TenantUser.Roles.Select(r => r.Role!).Where(r => r != null);
        }
        return Array.Empty<Role>();
    }

    private static IEnumerable<UserRole> ResolveUserRoles(Credential credential) {
        if (credential.OwnerType == CredentialOwnerType.PlatformUser && credential.PlatformUser != null) {
            return credential.PlatformUser.Roles;
        }
        if (credential.OwnerType == CredentialOwnerType.TenantUser && credential.TenantUser != null) {
            return credential.TenantUser.Roles;
        }
        return Array.Empty<UserRole>();
    }

    // JWT compact serialization is exactly `header.payload.signature` — 3 non-empty
    // segments separated by dots. Opaque session tokens are base64url(32 bytes) and
    // contain no dots. A non-JWT input lets us re-route to the opaque-session path
    // before invoking the JWT parser (which throws IDX12741 on malformed values).
    private static bool LooksLikeJwt(string value) {
        var parts = value.Split('.');
        return parts.Length == 3 && !string.IsNullOrEmpty(parts[0]) && !string.IsNullOrEmpty(parts[1]) && !string.IsNullOrEmpty(parts[2]);
    }

    private string? ResolveToken() {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) {
            return authHeader.Substring("Bearer ".Length).Trim();
        }

        var cookie = Request.Cookies["auth_token"];
        if (!string.IsNullOrWhiteSpace(cookie)) {
            return cookie;
        }

        if (Request.Query.TryGetValue("token", out var tokenValues)) {
            return tokenValues.FirstOrDefault();
        }

        return null;
    }
}
