using AuthApi.Contracts.Requests;
using AuthApi.Contracts.Responses;
using AuthApi.Infrastructure.Authorization;
using AuthApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Continuo.Observability.Attributes;

namespace AuthApi.Controllers;

[ApiController]
[Route("auth/screens")]
public class ScreensController : ControllerBase {
    private readonly AuthDbContext _db;

    public ScreensController(AuthDbContext db) {
        _db = db;
    }

    [HttpGet("apps")]
    [RequirePermission("platform.auth.roles.manage")]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> Apps() {
        var apps = await _db.Screens
            .AsNoTracking()
            .GroupBy(s => s.AppCode)
            .Select(g => new AppScreensResponse(
                g.Key,
                g.OrderBy(s => s.ScreenKey).Select(s => new { id = s.Id.ToString(), s.ScreenKey, s.Title })))
            .ToListAsync();
        return Ok(apps);
    }

    [HttpGet]
    [RequirePermission("platform.auth.screens.manage")]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> List([FromQuery] string? app) {
        var query = _db.Screens.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(app)) {
            query = query.Where(s => s.AppCode == app);
        }
        var items = await query
            .OrderBy(s => s.AppCode)
            .ThenBy(s => s.ScreenKey)
            .Select(s => new {
                s.Id,
                s.AppCode,
                s.ScreenKey,
                s.Title,
                s.Description,
                s.RequiredPermissionsJson,
                s.IsSystem
            })
            .ToListAsync();
        return Ok(items);
    }

    [HttpPost]
    [RequirePermission("platform.auth.screens.manage")]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> Upsert([FromBody] UpsertScreenRequest req, CancellationToken ct) {
        var normalizedKey = req.ScreenKey.Trim();
        var screen = await _db.Screens.FirstOrDefaultAsync(s => s.AppCode == req.AppCode && s.ScreenKey == normalizedKey, ct);
        var requiredJson = System.Text.Json.JsonSerializer.Serialize(req.RequiredPermissions ?? Array.Empty<string>());

        if (screen == null) {
            screen = new Screen {
                AppCode = req.AppCode,
                ScreenKey = normalizedKey,
                Title = req.Title,
                Description = req.Description,
                RequiredPermissionsJson = requiredJson,
                IsSystem = req.IsSystem
            };
            _db.Screens.Add(screen);
        }
        else {
            screen.Title = req.Title;
            screen.Description = req.Description;
            screen.RequiredPermissionsJson = requiredJson;
            screen.IsSystem = req.IsSystem;
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { screen.Id });
    }

    [HttpGet("assignments")]
    [RequirePermission("platform.auth.screens.manage")]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> Assignments([FromQuery] string? platformUserId) {
        var query = _db.ScreenUsers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(platformUserId)) {
            query = query.Where(su => su.PlatformUserId == Ulid.Parse(platformUserId));
        }

        var items = await query
            .OrderByDescending(su => su.CreatedAtUtc)
            .Select(su => new {
                su.Id,
                su.ScreenId,
                su.PlatformUserId,
                su.TenantId,
                su.CreatedAtUtc,
                su.ExpiresAtUtc,
                su.CreatedBy
            })
            .ToListAsync();
        return Ok(items);
    }

    [HttpPost("assign")]
    [RequirePermission("platform.auth.screens.manage")]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> Assign([FromBody] AssignRequest req, CancellationToken ct) {
        var entity = new ScreenUser {
            ScreenId = Ulid.Parse(req.ScreenId),
            PlatformUserId = Ulid.Parse(req.PlatformUserId),
            TenantId = string.IsNullOrWhiteSpace(req.TenantId) ? null : Ulid.Parse(req.TenantId),
            ExpiresAtUtc = req.ExpiresAtUtc,
            CreatedBy = User?.Identity?.Name ?? "system"
        };
        _db.ScreenUsers.Add(entity);
        await _db.SaveChangesAsync(ct);
        return Ok(new { entity.Id });
    }

    [HttpDelete("assign/{id}")]
    [RequirePermission("platform.auth.screens.manage")]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> DeleteAssignment([FromRoute] string id, CancellationToken ct) {
        var parsed = Ulid.Parse(id);
        var entity = await _db.ScreenUsers.FirstOrDefaultAsync(su => su.Id == parsed, ct);
        if (entity == null) {
            return NotFound();
        }

        _db.ScreenUsers.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }
}
