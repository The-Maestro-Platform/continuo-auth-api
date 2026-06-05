using AuthApi.Contracts.Requests;
using AuthApi.Infrastructure.Authorization;
using AuthApi.Models;
using AuthApi.Permissions;
using AuthApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Continuo.Observability.Attributes;

namespace AuthApi.Controllers;

[ApiController]
[Route("auth/roles")]
public class RolesController : ControllerBase {
    private readonly RolesService _roles;
    private readonly IConfiguration _configuration;
    // Role definition + screen/permission assignment is a platform-administration concern.
    // Tenant admins (TenantOwner/TenantAdmin) can only *assign existing roles to users*
    // (see TenantUsersController/CredentialsController), not author roles or grant screens.
    private static readonly string[] RolesManagePermissions = [
        PermissionKeys.Platform.AuthRolesManage
    ];

    public RolesController(RolesService roles, IConfiguration configuration) {
        _roles = roles;
        _configuration = configuration;
    }

    [HttpGet]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> List(
        [FromQuery] int take = 500,
        [FromQuery] RoleScope? scope = null,
        [FromQuery] bool includeScreens = false,
        CancellationToken ct = default) {
        var res = await _roles.ListAsync(take, scope, includeScreens, ct);
        return Ok(res);
    }

    [HttpPost]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> Create([FromBody] CreateRoleRequest request, CancellationToken ct) {
        var scope = request.Scope ?? RoleScope.Tenant;
        if (!CanManageScope(scope)) {
            return Forbid();
        }

        try {
            var permissionKeys = request.PermissionKeys ?? request.PermissionCodes;
            var dto = await _roles.CreateAsync(request.Name, scope, request.Description, permissionKeys, ct);
            return Created($"/auth/roles/{dto.GetType().GetProperty("id")?.GetValue(dto)}", dto);
        }
        catch (InvalidOperationException ex) {
            return Conflict(ex.Message);
        }
        catch (ArgumentException ex) {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id}/screens")]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> SetScreens([FromRoute] string id, [FromBody] UpdateRoleScreensRequest request, CancellationToken ct) {
        if (!Ulid.TryParse(id, out var roleId)) {
            return NotFound();
        }

        var scope = await _roles.GetScopeAsync(roleId, ct);
        if (scope is null) {
            return NotFound();
        }
        if (!CanManageScope(scope.Value)) {
            return Forbid();
        }

        var autoGrantedPermissions = await _roles.SetScreensAsync(roleId, request.ScreenIds ?? Array.Empty<string>(), ct);
        return Ok(new {
            id,
            screenIds = request.ScreenIds ?? Array.Empty<string>(),
            autoGrantedPermissionKeys = autoGrantedPermissions
        });
    }

    [HttpPut("{id}/permissions")]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> SetPermissions([FromRoute] string id, [FromBody] UpdateRolePermissionsRequest request, CancellationToken ct) {
        if (!Ulid.TryParse(id, out var roleId)) {
            return NotFound();
        }

        var scope = await _roles.GetScopeAsync(roleId, ct);
        if (scope is null) {
            return NotFound();
        }
        if (!CanManageScope(scope.Value)) {
            return Forbid();
        }

        var permissionKeys = await _roles.SetPermissionsAsync(roleId, request.PermissionKeys ?? Array.Empty<string>(), ct);
        return Ok(new { id, permissionKeys });
    }

    [HttpPut("{id}")]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> Update([FromRoute] string id, [FromBody] UpdateRoleRequest request, CancellationToken ct) {
        if (!Ulid.TryParse(id, out var roleId)) {
            return NotFound();
        }

        var scope = await _roles.GetScopeAsync(roleId, ct);
        if (scope is null) {
            return NotFound();
        }
        if (!CanManageScope(scope.Value)) {
            return Forbid();
        }

        try {
            var dto = await _roles.UpdateAsync(roleId, request.Name, request.Description, ct);
            return Ok(dto);
        }
        catch (InvalidOperationException ex) {
            return Conflict(ex.Message);
        }
        catch (ArgumentException ex) {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> Delete([FromRoute] string id, CancellationToken ct) {
        if (!Ulid.TryParse(id, out var roleId)) {
            return NotFound();
        }

        var scope = await _roles.GetScopeAsync(roleId, ct);
        if (scope is null) {
            return NotFound();
        }
        if (!CanManageScope(scope.Value)) {
            return Forbid();
        }

        try {
            await _roles.DeleteAsync(roleId, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) {
            return Conflict(ex.Message);
        }
    }

    private bool CanManageScope(RoleScope scope) {
        // Both platform- and tenant-scoped role definitions require platform-level authority.
        return PermissionAuthorization.HasAnyPermission(HttpContext, _configuration, RolesManagePermissions);
    }
}
