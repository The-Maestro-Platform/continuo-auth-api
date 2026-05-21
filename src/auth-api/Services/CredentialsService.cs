using AuthApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Services;

public class CredentialsService {
    private readonly AuthDbContext _db;
    public CredentialsService(AuthDbContext db) { _db = db; }

    public async Task<IReadOnlyList<object>> ListAsync(
        int take,
        string? tenantCode,
        IReadOnlyCollection<string>? allowedBranchCodes,
        CancellationToken ct) {
        var size = Continuo.Shared.Pagination.Paging.NormalizePageSize(take, 500, 10, 5000);
        var query = _db.Credentials
            .Include(c => c.PlatformUser).ThenInclude(u => u!.Roles).ThenInclude(r => r.Role!)
            .Include(c => c.TenantUser).ThenInclude(u => u!.Tenant)
            .Include(c => c.TenantUser).ThenInclude(u => u!.Roles).ThenInclude(r => r.Role!)
            .Include(c => c.Customer).ThenInclude(cust => cust!.Tenant)
            .AsNoTracking()
            .Where(c => c.TenantUser == null || c.TenantUser.Status != TenantUserStatus.Deleted)
            .AsQueryable();

        var normalizedTenant = tenantCode?.Trim();
        if (!string.IsNullOrWhiteSpace(normalizedTenant)) {
            query = query.Where(c => c.TenantUser != null && c.TenantUser.Tenant.Code == normalizedTenant);
        }

        if (allowedBranchCodes is { Count: > 0 }) {
            var normalizedBranches = allowedBranchCodes
                .Select(code => code.Trim().ToLowerInvariant())
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct()
                .ToArray();

            if (normalizedBranches.Length > 0) {
                query = query.Where(c =>
                    c.TenantUser != null &&
                    c.TenantUser.Roles.Any(role =>
                        role.BranchCode != null &&
                        normalizedBranches.Contains(role.BranchCode.ToLower())));
            }
        }

        var credentials = await query
            .OrderBy(c => c.Login)
            .Take(size)
            .ToListAsync(ct);

        return credentials.Select(c => new {
            id = c.Id.ToString(),
            c.Login,
            email = c.Email,
            type = c.OwnerType,
            owner = BuildOwnerSummary(c)
        } as object).ToList();
    }

    private static object? BuildOwnerSummary(Credential credential) {
        return credential.OwnerType switch {
            CredentialOwnerType.PlatformUser when credential.PlatformUser != null => new {
                userType = UserType.PlatformUser,
                credential.PlatformUser.Id,
                credential.PlatformUser.DisplayName,
                credential.PlatformUser.Email,
                roles = credential.PlatformUser.Roles?.Select(r => r.Role.Name) ?? Enumerable.Empty<string>()
            },
            CredentialOwnerType.TenantUser when credential.TenantUser != null => new {
                userType = UserType.TenantUser,
                credential.TenantUser.Id,
                credential.TenantUser.DisplayName,
                credential.TenantUser.Email,
                tenant = credential.TenantUser.Tenant != null
                    ? new { credential.TenantUser.Tenant.Code, credential.TenantUser.Tenant.Name }
                    : null,
                roles = credential.TenantUser.Roles?.Select(r => r.Role.Name) ?? Enumerable.Empty<string>(),
                branchRoles = credential.TenantUser.Roles?.Select(r => new {
                    roleId = r.RoleId.ToString(),
                    roleName = r.Role.Name,
                    branchCode = r.BranchCode
                }) ?? Enumerable.Empty<object>()
            },
            CredentialOwnerType.Customer when credential.Customer != null => new {
                userType = UserType.Customer,
                credential.Customer.Id,
                credential.Customer.DisplayName,
                credential.Customer.Email,
                tenant = credential.Customer.Tenant != null
                    ? new { credential.Customer.Tenant.Code, credential.Customer.Tenant.Name }
                    : null
            },
            _ => null
        };
    }
}
