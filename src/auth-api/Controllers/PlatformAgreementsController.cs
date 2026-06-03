using AuthApi.Contracts.Requests;
using AuthApi.Contracts.Responses;
using AuthApi.Infrastructure.Authorization;
using AuthApi.Permissions;
using AuthApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Continuo.Observability.Attributes;
using Continuo.Shared.Security;

namespace AuthApi.Controllers;

/// <summary>
/// Platform-level legal agreements (Kullanım Koşulları / KVKK / Pazarlama
/// İzni) shown to customers at signup/login.
/// <para>
/// Routes:
/// <list type="bullet">
///   <item><c>GET  /auth/platform-agreements/active</c> — anonymous, public read of current active set.</item>
///   <item><c>GET  /auth/admin/platform-agreements</c> — admin list (incl. inactive history).</item>
///   <item><c>GET  /auth/admin/platform-agreements/{id}</c> — admin single.</item>
///   <item><c>POST /auth/admin/platform-agreements</c> — publish new version (deactivates old).</item>
///   <item><c>PUT  /auth/admin/platform-agreements/{id}</c> — in-place edit.</item>
///   <item><c>POST /auth/admin/platform-agreements/{id}/activate</c> — rollback to historical version.</item>
///   <item><c>DELETE /auth/admin/platform-agreements/{id}</c> — delete inactive row.</item>
/// </list>
/// </para>
/// </summary>
[ApiController]
public class PlatformAgreementsController : ControllerBase {
    private readonly PlatformAgreementsService _service;
    private readonly IConfiguration _configuration;
    private static readonly PlatformRole[] AdminRoles = { PlatformRole.PlatformOwner, PlatformRole.PlatformAdmin };

    public PlatformAgreementsController(PlatformAgreementsService service, IConfiguration configuration) {
        _service = service;
        _configuration = configuration;
    }

    // ------------------------------------------------------------------
    // Public — anonymous: qrmenu-mobile AgreementsModal + qrmenu-web /consent
    // ------------------------------------------------------------------
    [HttpGet("auth/platform-agreements/active")]
    [AllowAnonymous]
    [ContinuoProxyMethod("ui")]
    public async Task<ActionResult<List<PlatformAgreementResponse>>> ListActive(CancellationToken ct) {
        var rows = await _service.ListActiveAsync(ct);
        return Ok(rows);
    }

    // ------------------------------------------------------------------
    // Admin (tc-ops-ui) — requires platform.agreements.manage permission.
    // PlatformOwner/PlatformAdmin role check is left as a defense-in-depth
    // backup; permission-driven is the primary gate.
    // ------------------------------------------------------------------
    [HttpGet("auth/admin/platform-agreements")]
    [Authorize]
    [ContinuoProxyMethod("ui")]
    public async Task<ActionResult<List<PlatformAgreementResponse>>> ListAll(CancellationToken ct) {
        if (!HasAdminAccess()) return Forbid();
        var rows = await _service.ListAllAsync(ct);
        return Ok(rows);
    }

    [HttpGet("auth/admin/platform-agreements/{id}")]
    [Authorize]
    [ContinuoProxyMethod("ui")]
    public async Task<ActionResult<PlatformAgreementResponse>> GetById([FromRoute] string id, CancellationToken ct) {
        if (!HasAdminAccess()) return Forbid();
        if (!Ulid.TryParse(id, out var ulid)) return NotFound();
        var row = await _service.GetByIdAsync(ulid, ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpPost("auth/admin/platform-agreements")]
    [Authorize]
    [ContinuoProxyMethod("ui")]
    public async Task<ActionResult<PlatformAgreementResponse>> Create(
        [FromBody] CreatePlatformAgreementRequest request,
        CancellationToken ct
    ) {
        if (!HasAdminAccess()) return Forbid();
        try {
            var row = await _service.CreateAsync(request, GetActorLogin(), ct);
            return Created($"/auth/admin/platform-agreements/{row.Id}", row);
        }
        catch (InvalidOperationException ex) {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex) {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("auth/admin/platform-agreements/{id}")]
    [Authorize]
    [ContinuoProxyMethod("ui")]
    public async Task<ActionResult<PlatformAgreementResponse>> Update(
        [FromRoute] string id,
        [FromBody] UpdatePlatformAgreementRequest request,
        CancellationToken ct
    ) {
        if (!HasAdminAccess()) return Forbid();
        if (!Ulid.TryParse(id, out var ulid)) return NotFound();
        try {
            var row = await _service.UpdateAsync(ulid, request, GetActorLogin(), ct);
            return row is null ? NotFound() : Ok(row);
        }
        catch (ArgumentException ex) {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("auth/admin/platform-agreements/{id}/activate")]
    [Authorize]
    [ContinuoProxyMethod("ui")]
    public async Task<ActionResult<PlatformAgreementResponse>> Activate([FromRoute] string id, CancellationToken ct) {
        if (!HasAdminAccess()) return Forbid();
        if (!Ulid.TryParse(id, out var ulid)) return NotFound();
        var row = await _service.ActivateAsync(ulid, GetActorLogin(), ct);
        return row is null ? NotFound() : Ok(row);
    }

    [HttpDelete("auth/admin/platform-agreements/{id}")]
    [Authorize]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> Delete([FromRoute] string id, CancellationToken ct) {
        if (!HasAdminAccess()) return Forbid();
        if (!Ulid.TryParse(id, out var ulid)) return NotFound();
        try {
            var ok = await _service.DeleteAsync(ulid, ct);
            return ok ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex) {
            return Conflict(new { message = ex.Message });
        }
    }

    // ------------------------------------------------------------------
    // Defense-in-depth: permission OR role; either alone is sufficient.
    // Mirrors the CustomersController pattern.
    // ------------------------------------------------------------------
    private bool HasAdminAccess() =>
        PermissionAuthorization.HasAnyPermission(HttpContext, _configuration, PermissionKeys.Platform.AgreementsManage)
        || Continuo.Shared.Security.ClaimsHelper.HasAnyRole(HttpContext, AdminRoles);

    private string? GetActorLogin() =>
        HttpContext.User?.FindFirst("login")?.Value
        ?? HttpContext.User?.Identity?.Name;
}
