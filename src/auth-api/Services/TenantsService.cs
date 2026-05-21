using Microsoft.EntityFrameworkCore;

namespace AuthApi.Services;

public class TenantsService {
    private readonly AuthDbContext _db;
    public TenantsService(AuthDbContext db) { _db = db; }

    public async Task<IReadOnlyList<object>> ListAsync(int take, CancellationToken ct) {
        var size = Continuo.Shared.Pagination.Paging.NormalizePageSize(take, 500, 10, 5000);
        var tenants = await _db.Tenants.AsNoTracking().OrderBy(t => t.Name).Take(size).ToListAsync(ct);
        return tenants.Select(t => new {
            id = t.Id.ToString(),
            t.Code,
            t.Name,
            status = t.Status,
            t.Slug,
            t.Subdomain,
            t.ContactEmail,
            t.ContactPhone,
            t.Notes,
            t.CreatedAtUtc,
            t.UpdatedAtUtc
        } as object).ToList();
    }
}
