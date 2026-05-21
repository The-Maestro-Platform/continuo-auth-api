using AuthApi.Models;
using Microsoft.EntityFrameworkCore;
using Continuo.Configuration.Abstractions;
using Continuo.Configuration.Models;
using Continuo.Shared.Env;

namespace AuthApi.Services;

public class UsersService {
    private readonly AuthDbContext _db;
    private readonly IParameterProvider _parameters;

    public UsersService(AuthDbContext db, IParameterProvider parameters) {
        _db = db;
        _parameters = parameters;
    }

    public async Task<IReadOnlyList<UserListItemDto>> ListAsync(string? tenantCode, int? takeOverride, CancellationToken ct) {
        IQueryable<TenantUser> query = _db.TenantUsers
            .Include(u => u.Tenant)
            .Include(u => u.Roles).ThenInclude(r => r.Role)
            .Include(u => u.Credentials)
            .AsNoTracking();

        var normalizedTenant = tenantCode?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedTenant)) {
            query = query.Where(u => u.Tenant.Code == normalizedTenant);
        }

        var defaultPageSize = await ResolvePageSizeAsync(normalizedTenant, ct);
        var size = Continuo.Shared.Pagination.Paging.NormalizePageSize(takeOverride ?? defaultPageSize, defaultPageSize, 50, 2000);
        var users = await query.OrderBy(u => u.DisplayName).Take(size).ToListAsync(ct);

        return users.Select(u => new UserListItemDto {
            Id = u.Id.ToString(),
            DisplayName = u.DisplayName,
            FirstName = u.FirstName,
            LastName = u.LastName,
            Email = u.Email,
            PhoneNumber = u.PhoneNumber,
            AddressLine1 = u.AddressLine1,
            AddressLine2 = u.AddressLine2,
            City = u.City,
            Country = u.Country,
            PostalCode = u.PostalCode,
            PositionTitle = u.PositionTitle,
            MarketingOptIn = u.MarketingOptIn,
            Status = u.Status,
            Tenant = new TenantSlim { Id = u.TenantId.ToString(), Code = u.Tenant.Code, Name = u.Tenant.Name },
            Roles = u.Roles.Select(r => new RoleSlim { Id = r.RoleId.ToString(), Name = r.Role.Name }).ToList(),
            CredentialIds = u.Credentials.Select(c => c.Id.ToString()).ToList()
        }).ToList();
    }

    private async Task<int> ResolvePageSizeAsync(string? tenantCode, CancellationToken ct) {
        var scope = new ParameterScope {
            Environment = EnvUtil.ResolveEnvironmentName(),
            TenantCode = tenantCode?.Trim()
        };

        var parameter = await _parameters.GetAsync("auth-console", "users-page-size", scope, "users", ct);
        var resolved = parameter?.AsInt(250) ?? 250;
        return Math.Clamp(resolved, 50, 2000);
    }

    public async Task<bool> UpdateUserAsync(Ulid userId, UpdateUserData data, bool isSelf, CancellationToken ct) {
        var user = await _db.TenantUsers.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user == null) {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(data.DisplayName)) {
            user.DisplayName = data.DisplayName.Trim();
        }

        user.FirstName = data.FirstName?.Trim();
        user.LastName = data.LastName?.Trim();
        user.Email = data.Email?.Trim();
        user.PhoneNumber = data.PhoneNumber?.Trim();
        user.AddressLine1 = data.AddressLine1?.Trim();
        user.AddressLine2 = data.AddressLine2?.Trim();
        user.City = data.City?.Trim();
        user.Country = data.Country?.Trim();
        user.PostalCode = data.PostalCode?.Trim();
        user.PositionTitle = data.PositionTitle?.Trim();

        if (data.MarketingOptIn.HasValue) {
            user.MarketingOptIn = data.MarketingOptIn.Value;
        }

        user.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
