using System.Security.Cryptography;
using System.Text;
using AuthApi.Models;
using AuthApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Controllers;

/// <summary>
/// Server-side session ↔ internal JWT bridge.
///
/// Browser cookie now holds an opaque session token (~43 chars base64url),
/// not a JWT. When a BFF needs to call a downstream service with a Bearer
/// token, it POSTs the opaque token here and receives a fresh short-lived
/// JWT with the full claim set (incl. permissions). The endpoint is locked
/// behind the platform M2M API key so external callers cannot directly
/// upgrade a stolen session cookie into a Bearer token.
///
/// Rate-limit / abuse note: each call also bumps UserSession.LastSeenAtUtc,
/// which doubles as an idle-timeout signal. Revoked sessions short-circuit
/// to 401 so callers know to drop their cached JWT.
/// </summary>
[ApiController]
[Route("auth/session")]
public class SessionExchangeController : ControllerBase {
    private readonly AuthDbContext _db;
    private readonly ISessionService _sessionService;
    private readonly ITokenService _tokenService;
    private readonly IScreenAccessService _screenAccess;
    private readonly IConfiguration _config;
    private readonly ILogger<SessionExchangeController> _logger;

    public SessionExchangeController(
        AuthDbContext db,
        ISessionService sessionService,
        ITokenService tokenService,
        IScreenAccessService screenAccess,
        IConfiguration config,
        ILogger<SessionExchangeController> logger) {
        _db = db;
        _sessionService = sessionService;
        _tokenService = tokenService;
        _screenAccess = screenAccess;
        _config = config;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpPost("exchange")]
    public async Task<IActionResult> Exchange(CancellationToken ct) {
        if (!IsTrustedM2MCaller()) {
            _logger.LogWarning(
                "Session exchange rejected: caller did not present a valid X-M2M-API-KEY (remote {Remote})",
                HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new { message = "M2M key required" });
        }

        var opaqueToken = ResolveOpaqueSessionToken();
        if (string.IsNullOrWhiteSpace(opaqueToken)) {
            return Unauthorized(new { message = "Missing session token" });
        }

        var session = await _sessionService.ResolveByOpaqueTokenAsync(opaqueToken, ct);
        if (session == null) {
            return Unauthorized(new { message = "session_invalid", reason = "session_invalid" });
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
            .Include(c => c.Customer)
            .FirstOrDefaultAsync(c => c.Id == session.CredentialId, ct);

        if (credential == null || !credential.IsActive) {
            return Unauthorized(new { message = "credential_inactive" });
        }

        var ownerRoles = ResolveOwnerRoles(credential).ToList();
        var userRoles = ResolveUserRoles(credential).ToList();
        var roleNames = ownerRoles.Select(r => r.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var rolePermissions = ownerRoles.SelectMany(r => r.Permissions.Select(p => p.PermissionKey));
        var permissionKeys = (credential.OwnerType == CredentialOwnerType.Customer
                ? rolePermissions.Concat(new[] {
                    Permissions.PermissionKeys.Customer.MaestroUse,
                    Permissions.PermissionKeys.Customer.MaestroBillingManage
                  })
                : rolePermissions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var branchCodes = userRoles
            .Where(ur => !string.IsNullOrWhiteSpace(ur.BranchCode))
            .Select(ur => ur.BranchCode!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var branchRoleClaims = userRoles
            .Where(ur => !string.IsNullOrWhiteSpace(ur.BranchCode))
            .Select(ur => $"{ur.BranchCode}:{ur.Role.Name}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var appCode = session.AppId;
        var screens = await _screenAccess.ResolveScreensAsync(credential, permissionKeys, ownerRoles, appCode, ct);

        var (accessToken, accessExpires) = await _tokenService.CreateAccessTokenAsync(
            credential, roleNames, permissionKeys, screens, branchCodes, branchRoleClaims, session.Id);

        return Ok(new {
            accessToken,
            accessExpires,
            sessionId = session.Id.ToString(),
            appId = session.AppId
        });
    }

    private string? ResolveOpaqueSessionToken() {
        var raw = HttpContext.Request.Headers["X-Session-Token"].FirstOrDefault();
        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
    }

    private bool IsTrustedM2MCaller() {
        var providedKey = HttpContext.Request.Headers["X-M2M-API-KEY"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedKey)) {
            return false;
        }

        var expectedKey = _config["M2M:ApiKey"]
                          ?? _config["M2M__API_KEY"]
                          ?? _config["M2M_API_KEY"];
        if (string.IsNullOrWhiteSpace(expectedKey)) {
            return false;
        }

        var providedBytes = Encoding.UTF8.GetBytes(providedKey);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);
        return providedBytes.Length == expectedBytes.Length
               && CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
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
}
