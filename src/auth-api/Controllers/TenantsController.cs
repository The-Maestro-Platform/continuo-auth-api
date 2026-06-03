using AuthApi.Infrastructure.Authorization;
using AuthApi.Models;
using AuthApi.Permissions;
using AuthApi.Services;
using Microsoft.AspNetCore.Mvc;
using Continuo.Observability.Attributes;
using Continuo.Shared.Security;

namespace AuthApi.Controllers;

[ApiController]
[Route("auth/tenants")]
[AuthorizeUserType(UserType.PlatformUser)]
public class TenantsController : ControllerBase {
    private readonly TenantsService _tenants;
    private readonly IConfiguration _configuration;
    private static readonly PlatformRole[] ManagementRoles = { PlatformRole.PlatformOwner, PlatformRole.PlatformAdmin };
    private static readonly string[] ManagementPermissions = [
        PermissionKeys.Platform.TenantsManage
    ];

    public TenantsController(TenantsService tenants, IConfiguration configuration) {
        _tenants = tenants;
        _configuration = configuration;
    }

    [ContinuoProxyMethod("ui")]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int take = 500, CancellationToken ct = default) {
        var hasRoleAccess = ClaimsHelper.HasAnyRole(HttpContext, ManagementRoles);
        var hasPermissionAccess = PermissionAuthorization.HasAnyPermission(HttpContext, _configuration, ManagementPermissions);
        if (!hasRoleAccess && !hasPermissionAccess) {
            return Forbid();
        }
        var list = await _tenants.ListAsync(take, ct);
        return Ok(list);
    }
}
