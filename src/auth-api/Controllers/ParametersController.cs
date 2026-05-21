using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using AuthApi.Infrastructure.Authorization;
using AuthApi.Permissions;
using Continuo.Configuration.Models;
using Continuo.Configuration.Services;
using Continuo.Shared.Security;

namespace AuthApi.Controllers;

[ApiController]
[Route("auth/parameters")]
public class ParametersController : ControllerBase {
    private static readonly string[] PlatformParametersManagePermissions = [
        PermissionKeys.Platform.ParametersManage
    ];
    private static readonly string[] TenantParametersManagePermissions = [
        PermissionKeys.Tenant.ParametersManage
    ];
    private readonly ParameterDefinitionsService<AuthDbContext> _service;

    public ParametersController(ParameterDefinitionsService<AuthDbContext> service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ParameterDefinitionListQuery query, CancellationToken cancellationToken) {
        var tenantCode = ResolveTenantCode();
        var authResult = EnsureCanManageParameters(isTenantScoped: !string.IsNullOrWhiteSpace(tenantCode));
        if (authResult is not null) {
            return authResult;
        }

        if (!string.IsNullOrWhiteSpace(tenantCode)) {
            if (!string.IsNullOrWhiteSpace(query.TenantCode) && !IsTenantMatch(query.TenantCode, tenantCode)) {
                return Forbid();
            }
            query = query with { TenantCode = tenantCode };
        }

        var items = await _service.ListAsync(query, cancellationToken);
        return Ok(items);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById([FromRoute] string id, CancellationToken cancellationToken) {
        if (!Ulid.TryParse(id, out var parameterId)) {
            return NotFound();
        }

        var entity = await _service.GetByIdAsync(parameterId, cancellationToken);
        if (entity is null) {
            return NotFound();
        }

        var authResult = EnsureCanManageParameters(isTenantScoped: !string.IsNullOrWhiteSpace(entity.TenantCode));
        if (authResult is not null) {
            return authResult;
        }

        var tenantCode = ResolveTenantCode();
        if (!string.IsNullOrWhiteSpace(tenantCode) && !IsTenantMatch(entity.TenantCode, tenantCode)) {
            return Forbid();
        }
        return Ok(entity);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UpsertParameterDefinitionRequest request, CancellationToken cancellationToken) {
        var tenantCode = ResolveTenantCode();
        if (!string.IsNullOrWhiteSpace(tenantCode)) {
            if (!string.IsNullOrWhiteSpace(request.TenantCode) && !IsTenantMatch(request.TenantCode, tenantCode)) {
                return Forbid();
            }
            request = request with { TenantCode = tenantCode };
        }

        var authResult = EnsureCanManageParameters(isTenantScoped: !string.IsNullOrWhiteSpace(request.TenantCode));
        if (authResult is not null) {
            return authResult;
        }

        var (ok, conflict, result, error) = await _service.CreateAsync(request, ResolveActor(), cancellationToken);
        if (conflict) {
            return Conflict(new { message = error });
        }

        if (!ok) {
            return BadRequest(new { message = error ?? "Invalid request" });
        }

        return CreatedAtAction(nameof(GetById), new { id = result!.Id }, result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update([FromRoute] string id, [FromBody] UpsertParameterDefinitionRequest request, CancellationToken cancellationToken) {
        if (!Ulid.TryParse(id, out var parameterId)) {
            return NotFound();
        }

        var existing = await _service.GetByIdAsync(parameterId, cancellationToken);
        if (existing == null) {
            return NotFound();
        }

        var authResult = EnsureCanManageParameters(isTenantScoped: !string.IsNullOrWhiteSpace(existing.TenantCode));
        if (authResult is not null) {
            return authResult;
        }

        var tenantCode = ResolveTenantCode();
        if (!string.IsNullOrWhiteSpace(tenantCode)) {
            if (!IsTenantMatch(existing.TenantCode, tenantCode)) {
                return Forbid();
            }
            if (!string.IsNullOrWhiteSpace(request.TenantCode) && !IsTenantMatch(request.TenantCode, tenantCode)) {
                return Forbid();
            }
            request = request with { TenantCode = tenantCode };
        }

        var (ok, result, error) = await _service.UpdateAsync(parameterId, request, ResolveActor(), cancellationToken);
        if (!ok) {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete([FromRoute] string id, CancellationToken cancellationToken) {
        if (!Ulid.TryParse(id, out var parameterId)) {
            return NotFound();
        }

        var existing = await _service.GetByIdAsync(parameterId, cancellationToken);
        if (existing == null) {
            return NotFound();
        }

        var authResult = EnsureCanManageParameters(isTenantScoped: !string.IsNullOrWhiteSpace(existing.TenantCode));
        if (authResult is not null) {
            return authResult;
        }

        var tenantCode = ResolveTenantCode();
        if (!string.IsNullOrWhiteSpace(tenantCode)) {
            if (!IsTenantMatch(existing.TenantCode, tenantCode)) {
                return Forbid();
            }
        }

        var deleted = await _service.DeleteAsync(parameterId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    [AllowAnonymous]
    [HttpGet("modules/{module}/sections/{section}")]
    public async Task<IActionResult> GetForSection([FromRoute] string module, [FromRoute] string section, [FromQuery] ParameterScopeQuery query, CancellationToken cancellationToken) {
        var response = await _service.GetSectionForClientAsync(module, section, query, cancellationToken);

        Response.Headers["Cache-Control"] = "public, max-age=30";
        return Ok(response);
    }

    // Mapping now handled inside service; keep client response only for GetForSection

    // Client response mapping moved to service (ParameterClientValue)

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

    private IActionResult? EnsureCanManageParameters(bool isTenantScoped) {
        var user = HttpContext.User;
        if (user?.Identity?.IsAuthenticated != true) {
            return Unauthorized();
        }

        var configuration = HttpContext.RequestServices.GetService<IConfiguration>()
            ?? throw new InvalidOperationException("IConfiguration is not available.");

        if (PermissionAuthorization.HasAnyPermission(HttpContext, configuration, PlatformParametersManagePermissions)) {
            return null;
        }

        if (isTenantScoped &&
            PermissionAuthorization.HasAnyPermission(HttpContext, configuration, TenantParametersManagePermissions)) {
            return null;
        }

        return Forbid();
    }

    private static bool IsTenantMatch(string? incoming, string expected) {
        return TenantResolution.IsTenantMatch(incoming, expected);
    }
}
