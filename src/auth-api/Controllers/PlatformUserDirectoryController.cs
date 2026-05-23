using AuthApi.Data;
using AuthApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Continuo.Configuration.Extensions;
using Continuo.Observability.Attributes;

namespace AuthApi.Controllers;

/// <summary>
/// Service-to-service lookup yardımcısı. PlatformUsersController kullanıcı JWT'si
/// (user_type=PlatformUser) bekler; bu controller M2M-API-KEY ile başka servislerin
/// (ops-maestro-worker, notification-api) "InfraAdmin rolündeki platform user'lar
/// kim?" gibi sorgular yapmasına izin verir. UI'dan ulaşılmaz, gateway proxy
/// listesinde de değil.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("platform/users/directory")]
public sealed class PlatformUserDirectoryController : ControllerBase {
    private readonly AuthDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PlatformUserDirectoryController> _logger;

    public PlatformUserDirectoryController(
        AuthDbContext db,
        IConfiguration configuration,
        ILogger<PlatformUserDirectoryController> logger) {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Verilen platform rolüne sahip aktif PlatformUser'ları döner. Sadece email +
    /// displayName projection — minimal contract, başka servisler PII'ya gerek
    /// duymadan recipient listesi alabilsin.
    /// </summary>
    [HttpGet("by-role")]
    [ContinuoProxyMethod("internal")]
    public async Task<IActionResult> GetByRole(
        [FromQuery] string role,
        [FromQuery] int take = 100,
        CancellationToken ct = default) {
        if (!IsM2mAuthorized()) {
            return Unauthorized(new { error = "m2m_api_key_required" });
        }

        if (string.IsNullOrWhiteSpace(role)) {
            return BadRequest(new { error = "role_required" });
        }

        var roleName = role.Trim();
        take = Math.Clamp(take, 1, 500);

        var items = await _db.PlatformUsers
            .AsNoTracking()
            .Where(u => u.IsActive)
            .Where(u => u.Roles.Any(r =>
                r.Role.Scope == RoleScope.Platform &&
                r.Role.Name == roleName))
            .OrderBy(u => u.Email)
            .Take(take)
            .Select(u => new {
                email = u.Email,
                displayName = u.DisplayName
            })
            .ToListAsync(ct);

        _logger.LogDebug("platform/users/directory/by-role role={Role} count={Count}", roleName, items.Count);
        return Ok(new { role = roleName, count = items.Count, users = items });
    }

    private bool IsM2mAuthorized() {
        var configured = PlatformM2MConfiguration.ResolveApiKey(_configuration);
        if (string.IsNullOrWhiteSpace(configured)) {
            return false;
        }

        var provided = HttpContext.Request.Headers["X-M2M-API-KEY"].ToString();
        return !string.IsNullOrWhiteSpace(provided)
            && string.Equals(provided, configured, StringComparison.Ordinal);
    }
}
