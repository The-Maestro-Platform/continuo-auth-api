using AuthApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Controllers;

[ApiController]
[Route("api/permissions")]
[Authorize]
public class PermissionCatalogController : ControllerBase {
    private readonly AuthDbContext _context;

    public PermissionCatalogController(AuthDbContext context) {
        _context = context;
    }

    /// <summary>
    /// Get full permission catalog with metadata for UI rendering
    /// </summary>
    [HttpGet("catalog")]
    public async Task<ActionResult<PermissionCatalogResponse>> GetCatalog() {
        var permissions = await _context.Permissions
            .OrderBy(p => p.Scope)
            .ThenBy(p => p.Category)
            .ThenBy(p => p.SortOrder)
            .ThenBy(p => p.DisplayName)
            .Select(p => new PermissionDto {
                Key = p.Key,
                DisplayName = p.DisplayName,
                Description = p.Description,
                Scope = p.Scope.ToString(),
                Category = p.Category,
                Icon = p.Icon,
                SortOrder = p.SortOrder
            })
            .ToListAsync();

        return Ok(new PermissionCatalogResponse { Permissions = permissions });
    }

    /// <summary>
    /// Get permissions grouped by category
    /// </summary>
    [HttpGet("catalog/grouped")]
    public async Task<ActionResult<GroupedPermissionCatalogResponse>> GetGroupedCatalog() {
        var permissions = await _context.Permissions
            .OrderBy(p => p.Category)
            .ThenBy(p => p.SortOrder)
            .ThenBy(p => p.DisplayName)
            .Select(p => new PermissionDto {
                Key = p.Key,
                DisplayName = p.DisplayName,
                Description = p.Description,
                Scope = p.Scope.ToString(),
                Category = p.Category,
                Icon = p.Icon,
                SortOrder = p.SortOrder
            })
            .ToListAsync();

        var grouped = permissions
            .GroupBy(p => p.Category ?? "General")
            .ToDictionary(g => g.Key, g => g.ToList());

        return Ok(new GroupedPermissionCatalogResponse { Groups = grouped });
    }

    /// <summary>
    /// Get permissions by scope (Platform or Tenant)
    /// </summary>
    [HttpGet("catalog/scope/{scope}")]
    public async Task<ActionResult<PermissionCatalogResponse>> GetByScope(string scope) {
        if (!Enum.TryParse<RoleScope>(scope, true, out var roleScope)) {
            return BadRequest(new { error = "Invalid scope. Use 'Platform' or 'Tenant'" });
        }

        var permissions = await _context.Permissions
            .Where(p => p.Scope == roleScope)
            .OrderBy(p => p.Category)
            .ThenBy(p => p.SortOrder)
            .ThenBy(p => p.DisplayName)
            .Select(p => new PermissionDto {
                Key = p.Key,
                DisplayName = p.DisplayName,
                Description = p.Description,
                Scope = p.Scope.ToString(),
                Category = p.Category,
                Icon = p.Icon,
                SortOrder = p.SortOrder
            })
            .ToListAsync();

        return Ok(new PermissionCatalogResponse { Permissions = permissions });
    }
}

public class PermissionDto {
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Scope { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Icon { get; set; }
    public int SortOrder { get; set; }
}

public class PermissionCatalogResponse {
    public List<PermissionDto> Permissions { get; set; } = new();
}

public class GroupedPermissionCatalogResponse {
    public Dictionary<string, List<PermissionDto>> Groups { get; set; } = new();
}
