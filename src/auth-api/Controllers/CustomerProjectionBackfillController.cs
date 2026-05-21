using AuthApi.Contracts.Requests;
using AuthApi.Contracts.Responses;
using AuthApi.Infrastructure.Authorization;
using AuthApi.Models;
using AuthApi.Permissions;
using AuthApi.Services;
using Microsoft.AspNetCore.Mvc;
using Continuo.Shared.Security;

namespace AuthApi.Controllers;

[ApiController]
[Route("auth/customers/projection-backfill")]
[AuthorizeUserType(UserType.PlatformUser)]
public sealed class CustomerProjectionBackfillController(
    CustomerProjectionBackfillService backfill,
    IConfiguration configuration)
    : ControllerBase {
    private static readonly string[] ManagementRoles = ["PlatformOwner", "PlatformAdmin"];
    private static readonly string[] ManagementPermissions = [PermissionKeys.Platform.InfraManage];

    [HttpPost]
    [ProducesResponseType(typeof(CustomerProjectionBackfillResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Enqueue(
        [FromBody] CustomerProjectionBackfillRequest? request,
        CancellationToken ct) {
        var hasRoleAccess = ClaimsHelper.HasAnyRole(HttpContext, ManagementRoles);
        var hasPermissionAccess = PermissionAuthorization.HasAnyPermission(HttpContext, configuration, ManagementPermissions);
        if (!hasRoleAccess && !hasPermissionAccess) {
            return Forbid();
        }

        var response = await backfill.EnqueueRegisteredEventsAsync(
            request ?? new CustomerProjectionBackfillRequest(),
            ct);
        return Ok(response);
    }
}
