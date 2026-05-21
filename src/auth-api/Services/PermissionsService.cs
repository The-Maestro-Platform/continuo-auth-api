using AuthApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Services;

public class PermissionsService {
    private readonly AuthDbContext _db;
    public PermissionsService(AuthDbContext db) { _db = db; }

    public async Task<IReadOnlyList<PermissionDto>> ListAsync(int? take, RoleScope? scope, CancellationToken ct) {
        var size = Continuo.Shared.Pagination.Paging.NormalizePageSize(take ?? 500, 500, 10, 5000);
        var query = _db.Permissions.AsNoTracking();
        if (scope.HasValue) {
            query = query.Where(p => p.Scope == scope.Value);
        }
        return await query
            .OrderBy(p => p.Scope).ThenBy(p => p.DisplayName)
            .Take(size)
            .Select(p => new PermissionDto(p.Key, p.DisplayName, p.Description, p.Scope))
            .ToListAsync(ct);
    }
}
