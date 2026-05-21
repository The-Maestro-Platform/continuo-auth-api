using AuthApi.Contracts.Requests;
using AuthApi.Infrastructure;
using AuthApi.Infrastructure.Authorization;
using AuthApi.Models;
using Microsoft.AspNetCore.Mvc;

namespace AuthApi.Controllers;

[ApiController]
[Route("customer/orders")]
[AuthorizeUserType(UserType.Customer)]
public class CustomerOrdersController : ControllerBase {
    private readonly ITenantContext _tenantContext;

    public CustomerOrdersController(ITenantContext tenantContext) {
        _tenantContext = tenantContext;
    }

    [HttpPost]
    public IActionResult Create([FromBody] CreateOrderRequest request) {
        if (!_tenantContext.HasTenant) {
            return BadRequest("Tenant context is required");
        }

        return Ok(new {
            orderId = Ulid.NewUlid().ToString(),
            tenantId = _tenantContext.TenantId,
            request.ProductId,
            request.Quantity,
            request.Notes,
            placedAt = DateTime.UtcNow
        });
    }
}
