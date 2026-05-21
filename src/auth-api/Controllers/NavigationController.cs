using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AuthApi.Contracts.Responses;
using AuthApi.Data;
using AuthApi.Models;
using AuthApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Continuo.Observability.Attributes;

namespace AuthApi.Controllers;

/// <summary>
/// 2026-05-20: JWT'den `screen` claim'i kaldırıldı (cookie 4KB incident).
/// Bu controller eskiden `User.Claims["screen"]` üzerinden filter yapıyordu →
/// claim yokken liste boş → menüler hiç render edilmiyordu (mert.cengiz
/// dashboard boş ekran incident'i). Artık AuthClaimsController gibi
/// credential_id'den DB'ye gidip fresh screens listesini çekiyoruz.
/// </summary>
[ApiController]
[Route("auth/navigation")]
[Authorize]
public class NavigationController : ControllerBase {
    private readonly NavigationService _navigationService;
    private readonly AuthDbContext _db;
    private readonly IScreenAccessService _screenAccess;
    private readonly ILogger<NavigationController> _logger;

    public NavigationController(
        NavigationService navigationService,
        AuthDbContext db,
        IScreenAccessService screenAccess,
        ILogger<NavigationController> logger) {
        _navigationService = navigationService;
        _db = db;
        _screenAccess = screenAccess;
        _logger = logger;
    }

    [HttpGet]
    [ContinuoProxyMethod("ui")]
    public async Task<ActionResult<NavigationResponse>> GetNavigation(
        [FromQuery] string appCode = "console-admin",
        CancellationToken ct = default) {
        var screens = await ResolveAuthoritativeScreensAsync(appCode, ct);
        if (screens == null) {
            return Unauthorized(new { message = "Credential not found or inactive" });
        }
        var items = await _navigationService.GetNavigationAsync(appCode, screens);
        return Ok(new NavigationResponse { Items = items });
    }

    [HttpGet("grouped")]
    [ContinuoProxyMethod("ui")]
    public async Task<ActionResult<GroupedNavigationResponse>> GetGroupedNavigation(
        [FromQuery] string appCode = "console-admin",
        CancellationToken ct = default) {
        var screens = await ResolveAuthoritativeScreensAsync(appCode, ct);
        if (screens == null) {
            return Unauthorized(new { message = "Credential not found or inactive" });
        }
        var groups = await _navigationService.GetGroupedNavigationAsync(appCode, screens);
        return Ok(new GroupedNavigationResponse { Groups = groups });
    }

    private async Task<IReadOnlyList<string>?> ResolveAuthoritativeScreensAsync(
        string appCode,
        CancellationToken ct) {
        var credentialIdRaw = User.Claims.FirstOrDefault(c =>
            c.Type == ClaimTypes.NameIdentifier ||
            c.Type == JwtRegisteredClaimNames.Sub ||
            c.Type == "sub")?.Value;
        if (string.IsNullOrWhiteSpace(credentialIdRaw) || !Ulid.TryParse(credentialIdRaw, out var credentialId)) {
            _logger.LogWarning("Navigation request without valid credential subject claim.");
            return null;
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
            .FirstOrDefaultAsync(c => c.IsActive && c.Id == credentialId, ct);
        if (credential == null) {
            return null;
        }

        var ownerRoles = ResolveOwnerRoles(credential).ToList();
        var permissions = ownerRoles
            .SelectMany(r => r.Permissions.Select(p => p.PermissionKey))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return await _screenAccess.ResolveScreensAsync(credential, permissions, ownerRoles, appCode, ct);
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
}
