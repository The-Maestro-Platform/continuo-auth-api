using AuthApi.Contracts.Requests;
using AuthApi.Infrastructure;
using AuthApi.Infrastructure.Authorization;
using AuthApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace AuthApi.Controllers;

[ApiController]
[Route("tenant/robots")]
[AuthorizeUserType(UserType.TenantUser)]
[RequirePermission("tenant.robots.manage")]
public class TenantRobotsController : ControllerBase {
    private readonly ITenantContext _tenantContext;

    public TenantRobotsController(ITenantContext tenantContext) {
        _tenantContext = tenantContext;
    }

    [HttpPost("config")]
    public IActionResult UpdateConfig([FromBody] UpdateRobotConfigRequest request) {
        if (!_tenantContext.HasTenant) {
            return BadRequest("Tenant context is required");
        }

        return Ok(new {
            tenantId = _tenantContext.TenantId,
            tenantCode = _tenantContext.TenantCode,
            request.RobotId,
            request.LayoutVersion,
            updatedAt = DateTime.UtcNow
        });
    }
}
