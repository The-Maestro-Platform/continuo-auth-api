using AuthApi.Contracts.Requests;
using AuthApi.Infrastructure.Authorization;
using AuthApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Controllers;

[ApiController]
[Route("platform/tenants")]
[AuthorizeUserType(UserType.PlatformUser)]
[RequirePermission("platform.tenants.manage")]
public class PlatformManagementController : ControllerBase {
    private readonly AuthDbContext _db;

    public PlatformManagementController(AuthDbContext db) {
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest request, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) {
            return BadRequest("Code and Name are required");
        }

        var normalized = request.Code.Trim().ToLowerInvariant();
        if (await _db.Tenants.AnyAsync(t => t.Code == normalized, ct)) {
            return Conflict("Tenant already exists");
        }

        var tenant = new Tenant {
            Code = normalized,
            Name = request.Name.Trim(),
            Subdomain = request.Subdomain?.Trim().ToLowerInvariant(),
            ContactEmail = request.ContactEmail?.Trim(),
            ContactPhone = request.ContactPhone?.Trim()
        };

        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(ct);

        return Created($"/tenants/{tenant.Id}", new {
            id = tenant.Id.ToString(),
            tenant.Code,
            tenant.Name,
            tenant.Subdomain,
            tenant.Status
        });
    }
}
