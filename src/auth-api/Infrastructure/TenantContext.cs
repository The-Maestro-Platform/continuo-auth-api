using AuthApi.Models;
using Microsoft.EntityFrameworkCore;
using Continuo.Shared.Security;

namespace AuthApi.Infrastructure;

public class TenantContext : ITenantContext {
    private readonly AuthDbContext _db;
    private Tenant? _tenant;
    private string? _branchCode;

    public TenantContext(AuthDbContext db) {
        _db = db;
    }

    public Ulid? TenantId => _tenant?.Id;
    public string? TenantCode => _tenant?.Code;
    public string? BranchCode => _branchCode;
    public TenantStatus? Status => _tenant?.Status;
    public bool HasTenant => _tenant != null;

    public async Task EnsureResolvedAsync(HttpContext context, CancellationToken ct) {
        if (_tenant != null) {
            return;
        }

        _branchCode = TenantResolution.ResolveBranchId(
            context,
            TenantResolveSource.Header | TenantResolveSource.Query);

        var code = TenantResolution.ResolveTenantCode(
            context,
            TenantResolveSource.Header | TenantResolveSource.Query | TenantResolveSource.Claims | TenantResolveSource.Host);
        if (!string.IsNullOrWhiteSpace(code)) {
            _tenant = await _db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Code == code || t.Slug == code || t.Subdomain == code, ct);
        }
    }
}
