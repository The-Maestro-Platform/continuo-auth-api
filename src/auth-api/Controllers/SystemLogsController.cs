using AuthApi.Infrastructure.Authorization;
using AuthApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Controllers;

[ApiController]
[Route("api/system-logs")]
[Authorize]
[AuthorizeUserType(UserType.PlatformUser)]
[RequirePermission("platform.logs.view")]
public class SystemLogsController : ControllerBase {
    private readonly AuthDbContext _context;

    public SystemLogsController(AuthDbContext context) {
        _context = context;
    }

    /// <summary>
    /// Get system logs with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<SystemLogDto>>> GetLogs(
        [FromQuery] string? search = null,
        [FromQuery] string? entityType = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? tenantId = null,
        [FromQuery] int limit = 100) {
        var query = _context.SystemLogs.AsQueryable();

        // Filter by search term
        if (!string.IsNullOrWhiteSpace(search)) {
            query = query.Where(log =>
                log.Action.Contains(search) ||
                (log.EntityType != null && log.EntityType.Contains(search)) ||
                (log.EntityId != null && log.EntityId.Contains(search)));
        }

        // Filter by entity type
        if (!string.IsNullOrWhiteSpace(entityType)) {
            query = query.Where(log => log.EntityType == entityType);
        }

        // Filter by date range
        if (from.HasValue) {
            query = query.Where(log => log.Timestamp >= from.Value);
        }
        if (to.HasValue) {
            query = query.Where(log => log.Timestamp <= to.Value);
        }

        // Filter by tenant
        if (!string.IsNullOrWhiteSpace(tenantId)) {
            var tid = Ulid.Parse(tenantId);
            query = query.Where(log => log.TenantId == tid);
        }

        var logs = await query
            .OrderByDescending(log => log.Timestamp)
            .Take(Math.Min(limit, 1000)) // Max 1000 records
            .Select(log => new SystemLogDto {
                Id = log.Id.ToString(),
                UserId = log.UserId.HasValue ? log.UserId.Value.ToString() : null,
                Timestamp = log.Timestamp,
                Action = log.Action,
                EntityType = log.EntityType,
                EntityId = log.EntityId,
                Metadata = log.Metadata,
                TenantId = log.TenantId.HasValue ? log.TenantId.Value.ToString() : null
            })
            .ToListAsync();

        return Ok(logs);
    }

    /// <summary>
    /// Get logs for a specific user
    /// </summary>
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<List<SystemLogDto>>> GetLogsByUser(
        string userId,
        [FromQuery] int limit = 100) {
        var uid = Ulid.Parse(userId);
        var logs = await _context.SystemLogs
            .Where(log => log.UserId == uid)
            .OrderByDescending(log => log.Timestamp)
            .Take(Math.Min(limit, 1000))
            .Select(log => new SystemLogDto {
                Id = log.Id.ToString(),
                UserId = log.UserId.HasValue ? log.UserId.Value.ToString() : null,
                Timestamp = log.Timestamp,
                Action = log.Action,
                EntityType = log.EntityType,
                EntityId = log.EntityId,
                Metadata = log.Metadata,
                TenantId = log.TenantId.HasValue ? log.TenantId.Value.ToString() : null
            })
            .ToListAsync();

        return Ok(logs);
    }

    /// <summary>
    /// Get logs for a specific entity
    /// </summary>
    [HttpGet("entity/{entityType}/{entityId}")]
    public async Task<ActionResult<List<SystemLogDto>>> GetLogsByEntity(
        string entityType,
        string entityId,
        [FromQuery] int limit = 100) {
        var logs = await _context.SystemLogs
            .Where(log => log.EntityType == entityType && log.EntityId == entityId)
            .OrderByDescending(log => log.Timestamp)
            .Take(Math.Min(limit, 1000))
            .Select(log => new SystemLogDto {
                Id = log.Id.ToString(),
                UserId = log.UserId.HasValue ? log.UserId.Value.ToString() : null,
                Timestamp = log.Timestamp,
                Action = log.Action,
                EntityType = log.EntityType,
                EntityId = log.EntityId,
                Metadata = log.Metadata,
                TenantId = log.TenantId.HasValue ? log.TenantId.Value.ToString() : null
            })
            .ToListAsync();

        return Ok(logs);
    }
}

public class SystemLogDto {
    public string Id { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? UserEmail { get; set; }
    public DateTime Timestamp { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? Metadata { get; set; }
    public string? TenantId { get; set; }
    public string? TenantName { get; set; }
}
