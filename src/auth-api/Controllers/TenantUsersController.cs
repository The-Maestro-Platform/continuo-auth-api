using AuthApi.Contracts.Requests;
using AuthApi.Infrastructure;
using AuthApi.Infrastructure.Authorization;
using AuthApi.Models;
using AuthApi.Permissions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Continuo.Observability.Attributes;
using Continuo.Shared.Security;
using AuthTenantContext = AuthApi.Infrastructure.ITenantContext;

namespace AuthApi.Controllers;

[ApiController]
[Route("auth/tenant-users")]
[AuthorizeUserType(UserType.PlatformUser, UserType.TenantUser)]
public class TenantUsersController : ControllerBase {
    private readonly AuthDbContext _db;
    private readonly AuthTenantContext _tenantContext;
    private readonly IConfiguration _configuration;
    private static readonly string[] ViewPermissions = [
        PermissionKeys.Platform.AuthUsersView,
        PermissionKeys.Platform.AuthUsersManage,
        PermissionKeys.Tenant.UsersView,
        PermissionKeys.Tenant.UsersManage,
        PermissionKeys.Tenant.BranchManage
    ];
    private static readonly string[] ManagePermissions = [
        PermissionKeys.Platform.AuthUsersManage,
        PermissionKeys.Tenant.UsersManage,
        PermissionKeys.Tenant.BranchManage
    ];

    public TenantUsersController(AuthDbContext db, AuthTenantContext tenantContext, IConfiguration configuration) {
        _db = db;
        _tenantContext = tenantContext;
        _configuration = configuration;
    }

    [HttpGet]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> List([FromQuery] string? tenantCode, [FromQuery] int take = 500, CancellationToken ct = default) {
        if (!PermissionAuthorization.HasAnyPermission(HttpContext, _configuration, ViewPermissions)) {
            return Forbid();
        }

        var actorScope = TenantBranchAuthorization.Resolve(HttpContext, _tenantContext, _configuration);
        await LegacyBranchBackfill.ApplyForConsoleAdminAsync(
            _db,
            actorScope,
            _tenantContext.BranchCode,
            _configuration,
            ct);

        var requestedTenantCode = TenantBranchAuthorization.NormalizeTenantCode(tenantCode);

        string? effectiveTenantCode;
        if (actorScope.RequiresTenantScope) {
            if (string.IsNullOrWhiteSpace(actorScope.TenantCode)) {
                return Forbid();
            }

            if (!string.IsNullOrWhiteSpace(requestedTenantCode) &&
                !TenantBranchAuthorization.IsTenantMatch(requestedTenantCode, actorScope.TenantCode)) {
                return Forbid();
            }

            effectiveTenantCode = actorScope.TenantCode;
        }
        else if (actorScope.IsOwnerBypass || ClaimsHelper.HasAnyRole(HttpContext, "PlatformOwner", "PlatformAdmin")) {
            effectiveTenantCode = requestedTenantCode;
        }
        else {
            effectiveTenantCode = actorScope.TenantCode ?? requestedTenantCode;
            if (string.IsNullOrWhiteSpace(effectiveTenantCode)) {
                return Forbid();
            }
        }

        take = Math.Clamp(take, 1, 1000);
        var query = _db.TenantUsers
            .AsNoTracking()
            .Include(u => u.Tenant)
            .Include(u => u.Roles).ThenInclude(r => r.Role)
            .Include(u => u.Credentials)
            .Where(u => u.Status != TenantUserStatus.Deleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(effectiveTenantCode)) {
            var normalized = effectiveTenantCode.Trim();
            query = query.Where(u => u.Tenant.Code == normalized);
        }

        if (!actorScope.IsOwnerBypass && actorScope.BranchCodes.Count > 0) {
            var normalizedBranches = actorScope.BranchCodes
                .Select(code => code.ToLowerInvariant())
                .ToArray();

            query = query.Where(u => u.Roles.Any(role =>
                role.BranchCode != null &&
                normalizedBranches.Contains(role.BranchCode.ToLower())));
        }
        else if (actorScope.RequiresBranchScope) {
            return Forbid();
        }

        var items = await query
            .OrderBy(u => u.DisplayName)
            .Take(take)
            .Select(u => new {
                id = u.Id.ToString(),
                email = u.Email,
                displayName = u.DisplayName,
                status = u.Status,
                isActive = u.IsActive,
                tenant = new { id = u.TenantId.ToString(), u.Tenant.Code, u.Tenant.Name },
                roles = u.Roles.Select(r => r.Role.Name),
                credentials = u.Credentials.Select(c => new { id = c.Id.ToString(), c.Login, c.IsActive })
            })
            .ToListAsync(ct);

        return Ok(items);
    }

    [HttpPost]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> Create([FromBody] CreateTenantUserRequest req, CancellationToken ct) {
        if (!PermissionAuthorization.HasAnyPermission(HttpContext, _configuration, ManagePermissions)) {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password) || string.IsNullOrWhiteSpace(req.TenantCode)) {
            return BadRequest("Email, password and tenant code are required");
        }

        var actorScope = TenantBranchAuthorization.Resolve(HttpContext, _tenantContext, _configuration);
        await LegacyBranchBackfill.ApplyForConsoleAdminAsync(
            _db,
            actorScope,
            _tenantContext.BranchCode,
            _configuration,
            ct);

        var requestedTenantCode = TenantBranchAuthorization.NormalizeTenantCode(req.TenantCode);
        if (!CanAccessTenant(actorScope, requestedTenantCode)) {
            return Forbid();
        }

        if (!ValidateBranchAssignments(actorScope, req.BranchRoles, req.RoleIds)) {
            return Forbid();
        }

        var email = req.Email.Trim().ToLowerInvariant();
        var displayName = string.IsNullOrWhiteSpace(req.DisplayName) ? email : req.DisplayName.Trim();
        var tenantCode = requestedTenantCode!;

        // Pasif TenantUser/credential ayni mailde yeni aktif kayit eklemeyi bloklamaz.
        var exists = await _db.TenantUsers.AnyAsync(u => u.Email == email && u.Status == TenantUserStatus.Active, ct)
            || await _db.Credentials.AnyAsync(c => c.Login == email && c.IsActive, ct);
        if (exists) {
            return Conflict("User already exists");
        }

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Code == tenantCode || t.Slug == tenantCode, ct);
        if (tenant == null) {
            return BadRequest("TenantCode is not valid");
        }

        var user = new TenantUser {
            TenantId = tenant.Id,
            DisplayName = displayName,
            Email = email,
            Status = TenantUserStatus.Active
        };

        var credential = new Credential {
            Login = email,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            OwnerType = CredentialOwnerType.TenantUser,
            TenantUser = user,
            IsActive = true
        };

        user.Credentials.Add(credential);

        if (req.BranchRoles is { Length: > 0 }) {
            var roleIds = req.BranchRoles
                .Select(br => br.RoleId)
                .Where(rid => Ulid.TryParse(rid, out _))
                .Select(Ulid.Parse)
                .Distinct()
                .ToArray();
            var rolesMap = await _db.Roles
                .Where(r => roleIds.Contains(r.Id) && r.Scope == RoleScope.Tenant)
                .ToDictionaryAsync(r => r.Id, ct);

            foreach (var br in req.BranchRoles) {
                if (!Ulid.TryParse(br.RoleId, out var rid) || !rolesMap.TryGetValue(rid, out var role)) {
                    continue;
                }

                var normalizedBranch = TenantBranchAuthorization.NormalizeBranchCode(br.BranchCode);
                if (!TenantBranchAuthorization.IsBranchAllowed(
                        actorScope,
                        normalizedBranch,
                        rejectEmptyCodes: actorScope.RequiresBranchScope || actorScope.BranchCodes.Count > 0)) {
                    return Forbid();
                }

                user.Roles.Add(new UserRole {
                    Role = role,
                    TenantUser = user,
                    BranchCode = normalizedBranch
                });
            }
        }
        else if (req.RoleIds is { Length: > 0 }) {
            if (actorScope.RequiresBranchScope || (!actorScope.IsOwnerBypass && actorScope.BranchCodes.Count > 0)) {
                return Forbid();
            }

            var roleIds = req.RoleIds
                .Where(id => Ulid.TryParse(id, out _))
                .Select(Ulid.Parse)
                .ToArray();
            var roles = await _db.Roles.Where(r => roleIds.Contains(r.Id) && r.Scope == RoleScope.Tenant).ToListAsync(ct);
            foreach (var role in roles) {
                user.Roles.Add(new UserRole { Role = role, TenantUser = user });
            }
        }

        _db.TenantUsers.Add(user);
        _db.Credentials.Add(credential);
        await _db.SaveChangesAsync(ct);

        return Created($"/auth/tenant-users/{user.Id}", new { id = user.Id.ToString(), user.Email, user.DisplayName });
    }

    [HttpPatch("{id}/active")]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> SetActive([FromRoute] string id, [FromBody] SetActiveRequest req, CancellationToken ct) {
        if (!PermissionAuthorization.HasAnyPermission(HttpContext, _configuration, ManagePermissions)) {
            return Forbid();
        }

        if (!Ulid.TryParse(id, out var userId)) {
            return NotFound();
        }

        var user = await _db.TenantUsers
            .Include(u => u.Tenant)
            .Include(u => u.Roles)
            .Include(u => u.Credentials)
            .FirstOrDefaultAsync(u => u.Id == userId && u.Status != TenantUserStatus.Deleted, ct);
        if (user == null) {
            return NotFound();
        }

        var actorScope = TenantBranchAuthorization.Resolve(HttpContext, _tenantContext, _configuration);
        if (!CanManageTenantUser(actorScope, user)) {
            return Forbid();
        }

        user.Status = req.Active ? TenantUserStatus.Active : TenantUserStatus.Disabled;
        user.UpdatedAtUtc = DateTime.UtcNow;
        foreach (var cred in user.Credentials) {
            cred.IsActive = req.Active;
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { id = user.Id.ToString(), user.IsActive });
    }

    [HttpPut("{id}/roles")]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> SetRoles([FromRoute] string id, [FromBody] SetRolesRequest req, CancellationToken ct) {
        if (!PermissionAuthorization.HasAnyPermission(HttpContext, _configuration, ManagePermissions)) {
            return Forbid();
        }

        if (!Ulid.TryParse(id, out var userId)) {
            return NotFound();
        }

        var user = await _db.TenantUsers
            .Include(u => u.Tenant)
            .Include(u => u.Roles)
                .ThenInclude(r => r.Role)
            .FirstOrDefaultAsync(u => u.Id == userId && u.Status != TenantUserStatus.Deleted, ct);
        if (user == null) {
            return NotFound();
        }

        var actorScope = TenantBranchAuthorization.Resolve(HttpContext, _tenantContext, _configuration);
        if (!CanManageTenantUser(actorScope, user)) {
            return Forbid();
        }

        if (!ValidateBranchAssignments(actorScope, req.BranchRoles, req.RoleIds)) {
            return Forbid();
        }

        _db.UserRoles.RemoveRange(user.Roles);
        user.Roles.Clear();

        if (req.BranchRoles is { Length: > 0 }) {
            var roleIds = req.BranchRoles
                .Select(br => br.RoleId)
                .Where(rid => Ulid.TryParse(rid, out _))
                .Select(Ulid.Parse)
                .Distinct()
                .ToArray();
            var rolesMap = await _db.Roles
                .Where(r => roleIds.Contains(r.Id) && r.Scope == RoleScope.Tenant)
                .ToDictionaryAsync(r => r.Id, ct);

            foreach (var br in req.BranchRoles) {
                if (!Ulid.TryParse(br.RoleId, out var rid) || !rolesMap.TryGetValue(rid, out var role)) {
                    continue;
                }

                var normalizedBranch = TenantBranchAuthorization.NormalizeBranchCode(br.BranchCode);
                if (!TenantBranchAuthorization.IsBranchAllowed(
                        actorScope,
                        normalizedBranch,
                        rejectEmptyCodes: actorScope.RequiresBranchScope || actorScope.BranchCodes.Count > 0)) {
                    return Forbid();
                }

                user.Roles.Add(new UserRole {
                    Role = role,
                    TenantUser = user,
                    BranchCode = normalizedBranch
                });
            }
        }
        else {
            if (actorScope.RequiresBranchScope || (!actorScope.IsOwnerBypass && actorScope.BranchCodes.Count > 0)) {
                return Forbid();
            }

            var incoming = req.RoleIds?.Where(rid => Ulid.TryParse(rid, out _)).Select(Ulid.Parse).ToArray() ?? Array.Empty<Ulid>();
            var roles = incoming.Length == 0
                ? new List<Role>()
                : await _db.Roles.Where(r => incoming.Contains(r.Id) && r.Scope == RoleScope.Tenant).ToListAsync(ct);
            foreach (var role in roles) {
                user.Roles.Add(new UserRole { Role = role, TenantUser = user });
            }
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new {
            id = user.Id.ToString(),
            branchRoles = user.Roles.Select(r => new { roleId = r.RoleId.ToString(), roleName = r.Role.Name, r.BranchCode })
        });
    }

    [HttpPatch("{id}/password")]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> ResetPassword([FromRoute] string id, [FromBody] ResetPasswordRequest req, CancellationToken ct) {
        if (!PermissionAuthorization.HasAnyPermission(HttpContext, _configuration, ManagePermissions)) {
            return Forbid();
        }

        if (!Ulid.TryParse(id, out var userId)) {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6) {
            return BadRequest("Password too short");
        }

        var cred = await _db.Credentials
            .Include(c => c.TenantUser)
                .ThenInclude(u => u!.Tenant)
            .Include(c => c.TenantUser)
                .ThenInclude(u => u!.Roles)
            .FirstOrDefaultAsync(c => c.TenantUserId == userId && c.TenantUser != null && c.TenantUser.Status != TenantUserStatus.Deleted, ct);
        if (cred == null) {
            return NotFound();
        }

        var actorScope = TenantBranchAuthorization.Resolve(HttpContext, _tenantContext, _configuration);
        if (cred.TenantUser == null || !CanManageTenantUser(actorScope, cred.TenantUser)) {
            return Forbid();
        }

        cred.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
        cred.MustChangePassword = true;
        cred.PasswordChangedAtUtc = null;
        await _db.SaveChangesAsync(ct);

        return Ok(new { id = cred.Id.ToString() });
    }

    [HttpDelete("{id}")]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> SoftDelete([FromRoute] string id, CancellationToken ct) {
        if (!PermissionAuthorization.HasAnyPermission(HttpContext, _configuration, ManagePermissions)) {
            return Forbid();
        }

        if (!Ulid.TryParse(id, out var userId)) {
            return NotFound();
        }

        var user = await _db.TenantUsers
            .Include(u => u.Tenant)
            .Include(u => u.Roles)
            .Include(u => u.Credentials)
            .FirstOrDefaultAsync(u => u.Id == userId && u.Status != TenantUserStatus.Deleted, ct);
        if (user == null) {
            return NotFound();
        }

        var actorScope = TenantBranchAuthorization.Resolve(HttpContext, _tenantContext, _configuration);
        if (!CanManageTenantUser(actorScope, user)) {
            return Forbid();
        }

        user.Status = TenantUserStatus.Deleted;
        user.UpdatedAtUtc = DateTime.UtcNow;

        foreach (var credential in user.Credentials) {
            credential.IsActive = false;
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { id = user.Id.ToString(), deleted = true });
    }

    private static bool CanAccessTenant(TenantBranchActorScope actorScope, string? tenantCode) {
        if (actorScope.IsOwnerBypass) {
            return true;
        }

        if (string.IsNullOrWhiteSpace(tenantCode) || string.IsNullOrWhiteSpace(actorScope.TenantCode)) {
            return false;
        }

        return TenantBranchAuthorization.IsTenantMatch(actorScope.TenantCode, tenantCode);
    }

    private static bool CanManageTenantUser(TenantBranchActorScope actorScope, TenantUser user) {
        if (actorScope.IsOwnerBypass) {
            return true;
        }

        return TenantBranchAuthorization.IsTenantUserInScope(actorScope, user);
    }

    private static bool ValidateBranchAssignments(
        TenantBranchActorScope actorScope,
        BranchRoleAssignment[]? branchRoles,
        string[]? roleIds) {
        if (actorScope.IsOwnerBypass) {
            return true;
        }

        if (actorScope.RequiresBranchScope && actorScope.BranchCodes.Count == 0) {
            return false;
        }

        if (branchRoles is { Length: > 0 }) {
            return TenantBranchAuthorization.AreBranchesAllowed(
                actorScope,
                branchRoles.Select(role => role.BranchCode),
                rejectEmptyCodes: actorScope.RequiresBranchScope || actorScope.BranchCodes.Count > 0);
        }

        if (actorScope.RequiresBranchScope) {
            return false;
        }

        if (roleIds is { Length: > 0 } && actorScope.BranchCodes.Count > 0) {
            return false;
        }

        return true;
    }

}
