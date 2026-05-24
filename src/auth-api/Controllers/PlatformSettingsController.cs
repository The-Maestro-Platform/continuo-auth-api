using AuthApi.Infrastructure.Authorization;
using AuthApi.Models.PlatformSettings;
using AuthApi.Permissions;
using AuthApi.Services.PlatformSettings;
using Continuo.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace AuthApi.Controllers;

[ApiController]
[Route("auth/platform-settings")]
public class PlatformSettingsController : ControllerBase {
    private static readonly string[] PlatformSettingsManagePermissions = [
        PermissionKeys.Platform.SettingsManage,
    ];
    private static readonly string[] TenantSettingsManagePermissions = [
        PermissionKeys.Tenant.SettingsManage,
    ];

    private readonly IPlatformSettingsService _service;
    private readonly ILogger<PlatformSettingsController> _logger;

    public PlatformSettingsController(IPlatformSettingsService service, ILogger<PlatformSettingsController> logger) {
        _service = service;
        _logger = logger;
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) {
        var tenantCode = ResolveTenantCode();
        var dto = await _service.ResolveAsync(tenantCode, cancellationToken);
        // Same Cache-Control as the parameters section endpoint — 30s public.
        Response.Headers["Cache-Control"] = "public, max-age=30";
        return Ok(dto);
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] PlatformBrandingSettingsDto dto, CancellationToken cancellationToken) {
        var tenantCode = ResolveTenantCode();
        var authResult = EnsureCanManageSettings(isTenantScoped: !string.IsNullOrWhiteSpace(tenantCode));
        if (authResult is not null) {
            return authResult;
        }

        var updatedBy = ResolveActor();
        try {
            await _service.UpdateAsync(tenantCode, dto, updatedBy, cancellationToken);
        }
        catch (UpdateScopeViolationException ex) {
            return BadRequest(new { message = ex.Message });
        }

        var resolved = await _service.ResolveAsync(tenantCode, cancellationToken);
        return Ok(resolved);
    }

    private string ResolveActor() {
        var actor = HttpContext.User?.Identity?.Name;
        if (string.IsNullOrWhiteSpace(actor)) {
            actor = HttpContext.User?.FindFirst("sub")?.Value;
        }
        return actor ?? "system";
    }

    private string? ResolveTenantCode() {
        return TenantResolution.ResolveTenantCode(
            HttpContext,
            TenantResolveSource.Header | TenantResolveSource.Query | TenantResolveSource.Claims | TenantResolveSource.Host);
    }

    private IActionResult? EnsureCanManageSettings(bool isTenantScoped) {
        var user = HttpContext.User;
        if (user?.Identity?.IsAuthenticated != true) {
            return Unauthorized();
        }

        var configuration = HttpContext.RequestServices.GetService<IConfiguration>()
            ?? throw new InvalidOperationException("IConfiguration is not available.");

        // Platform owners can always write (both platform + any tenant scope).
        if (PermissionAuthorization.HasAnyPermission(HttpContext, configuration, PlatformSettingsManagePermissions)) {
            return null;
        }

        // Tenant scope writes also accept tenant.settings.manage.
        if (isTenantScoped
            && PermissionAuthorization.HasAnyPermission(HttpContext, configuration, TenantSettingsManagePermissions)) {
            return null;
        }

        return Forbid();
    }
}
