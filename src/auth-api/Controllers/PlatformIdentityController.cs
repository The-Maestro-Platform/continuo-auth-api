using AuthApi.Infrastructure.Authorization;
using AuthApi.Permissions;
using AuthApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Continuo.Observability.Attributes;
using Continuo.Shared.Security;

namespace AuthApi.Controllers;

/// <summary>
/// Single-row platform identity (legal company info). Drives the token
/// resolver used by <see cref="PlatformAgreementsController"/> so that
/// <c>{{companyName}}</c> / <c>{{companyEmail}}</c> etc. inside agreement
/// bodies are replaced with the operator-configured values before being
/// served to qrmenu-mobile / qrmenu-web.
/// </summary>
[ApiController]
public class PlatformIdentityController : ControllerBase {
    private readonly PlatformIdentityService _service;
    private readonly IConfiguration _configuration;
    private static readonly PlatformRole[] AdminRoles = { PlatformRole.PlatformOwner, PlatformRole.PlatformAdmin };

    public PlatformIdentityController(PlatformIdentityService service, IConfiguration configuration) {
        _service = service;
        _configuration = configuration;
    }

    /// <summary>Admin read — used by tc-ops-ui Şirket Bilgileri card.</summary>
    [HttpGet("auth/admin/platform-identity")]
    [Authorize]
    [ContinuoProxyMethod("ui")]
    public async Task<ActionResult<PlatformIdentityDto>> Get(CancellationToken ct) {
        if (!HasAdminAccess()) return Forbid();
        return Ok(await _service.GetAsync(ct));
    }

    /// <summary>Admin write — invalidates the 5dk identity cache. Token
    /// renderer picks up new values on the next agreement read.</summary>
    [HttpPut("auth/admin/platform-identity")]
    [Authorize]
    [ContinuoProxyMethod("ui")]
    public async Task<ActionResult<PlatformIdentityDto>> Update(
        [FromBody] UpdatePlatformIdentityRequest request,
        CancellationToken ct
    ) {
        if (!HasAdminAccess()) return Forbid();
        try {
            var dto = await _service.UpdateAsync(request, GetActorLogin(), ct);
            return Ok(dto);
        }
        catch (ArgumentException ex) {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Public token catalog — lets the editor cheatsheet list the
    /// available <c>{{tokens}}</c> with human-readable labels. Anonymous so
    /// the editor preview can fetch it without elevated auth.</summary>
    [HttpGet("auth/platform-identity/tokens")]
    [AllowAnonymous]
    [ContinuoProxyMethod("ui")]
    public ActionResult<IReadOnlyList<PlatformIdentityRenderer.TokenDescriptor>> ListTokens() {
        return Ok(PlatformIdentityRenderer.TokenCatalog);
    }

    private bool HasAdminAccess() =>
        PermissionAuthorization.HasAnyPermission(HttpContext, _configuration, PermissionKeys.Platform.AgreementsManage)
        || Continuo.Shared.Security.ClaimsHelper.HasAnyRole(HttpContext, AdminRoles);

    private string? GetActorLogin() =>
        HttpContext.User?.FindFirst("login")?.Value
        ?? HttpContext.User?.Identity?.Name;
}
