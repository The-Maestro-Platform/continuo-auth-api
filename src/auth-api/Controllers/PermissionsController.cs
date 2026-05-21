using AuthApi.Infrastructure.Authorization;
using AuthApi.Models;
using AuthApi.Permissions;
using Microsoft.AspNetCore.Mvc;
using Continuo.Observability.Attributes;

namespace AuthApi.Controllers;

[ApiController]
[Route("auth/permissions")]
[AuthorizeUserType(UserType.PlatformUser, UserType.TenantUser)]
public class PermissionsController : ControllerBase {
    private readonly AuthApi.Services.PermissionsService _service;
    private readonly IConfiguration _configuration;
    private static readonly string[] PlatformPermissions = [
        PermissionKeys.Platform.AuthRolesManage
    ];
    private static readonly string[] TenantPermissions = [
        PermissionKeys.Tenant.UsersManage,
        PermissionKeys.Tenant.UsersView,
        PermissionKeys.Tenant.BranchManage
    ];
    private static readonly string[] TenantAndPlatformPermissions = [
        .. PlatformPermissions,
        .. TenantPermissions
    ];

    public PermissionsController(AuthApi.Services.PermissionsService service, IConfiguration configuration) {
        _service = service;
        _configuration = configuration;
    }

    [HttpGet]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> List([FromQuery] int? take, [FromQuery] RoleScope? scope, CancellationToken ct) {
        if (!HasPermissionForScope(scope)) {
            return Forbid();
        }

        var list = await _service.ListAsync(take, scope, ct);
        return Ok(list);
    }

    private bool HasPermissionForScope(RoleScope? scope) {
        return scope switch {
            RoleScope.Platform => PermissionAuthorization.HasAnyPermission(HttpContext, _configuration, PlatformPermissions),
            RoleScope.Tenant => PermissionAuthorization.HasAnyPermission(HttpContext, _configuration, TenantAndPlatformPermissions),
            _ => PermissionAuthorization.HasAnyPermission(HttpContext, _configuration, TenantAndPlatformPermissions)
        };
    }
}
