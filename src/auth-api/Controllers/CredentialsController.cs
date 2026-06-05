using AuthApi.Contracts.Requests;
using AuthApi.Infrastructure;
using AuthApi.Infrastructure.Authorization;
using AuthApi.Models;
using AuthApi.Permissions;
using AuthApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Continuo.Observability.Attributes;
using Continuo.Shared.Security;
using AuthTenantContext = AuthApi.Infrastructure.ITenantContext;

namespace AuthApi.Controllers;

[ApiController]
[Route("auth/credentials")]
[AuthorizeUserType(UserType.PlatformUser, UserType.TenantUser)]
public class CredentialsController : ControllerBase {
    private readonly AuthDbContext _db;
    private readonly CredentialsService _creds;
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

    public CredentialsController(
        AuthDbContext db,
        CredentialsService creds,
        AuthTenantContext tenantContext,
        IConfiguration configuration) {
        _db = db;
        _creds = creds;
        _tenantContext = tenantContext;
        _configuration = configuration;
    }

    [HttpGet]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> List([FromQuery] int take = 500, [FromQuery] string? tenantCode = null, CancellationToken ct = default) {
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
        IReadOnlyCollection<string>? branchScope = null;

        if (actorScope.RequiresTenantScope) {
            if (string.IsNullOrWhiteSpace(actorScope.TenantCode)) {
                return Forbid();
            }

            if (!string.IsNullOrWhiteSpace(requestedTenantCode) &&
                !TenantBranchAuthorization.IsTenantAllowed(actorScope, requestedTenantCode)) {
                return Forbid();
            }

            effectiveTenantCode = actorScope.TenantCode;

            if (actorScope.RequiresBranchScope) {
                if (actorScope.BranchCodes.Count == 0) {
                    return Forbid();
                }

                branchScope = actorScope.BranchCodes.ToArray();
            }
        }
        else {
            // Only platform-level actors can view across tenants. A TenantOwner (IsOwnerBypass but
            // NOT IsPlatformBypass) is pinned to their own tenant code, ignoring any requested code.
            var canViewAllTenants = actorScope.IsPlatformBypass;
            effectiveTenantCode = canViewAllTenants ? requestedTenantCode : actorScope.TenantCode;

            if (!canViewAllTenants && string.IsNullOrWhiteSpace(effectiveTenantCode)) {
                return Forbid();
            }

            if (!actorScope.IsOwnerBypass && actorScope.BranchCodes.Count > 0) {
                branchScope = actorScope.BranchCodes.ToArray();
            }
        }

        var result = await _creds.ListAsync(take, effectiveTenantCode, branchScope, ct);
        return Ok(result);
    }

    [HttpPost]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> Create([FromBody] CreateCredentialRequest request, CancellationToken ct = default) {
        if (!PermissionAuthorization.HasAnyPermission(HttpContext, _configuration, ManagePermissions)) {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.Login) || string.IsNullOrWhiteSpace(request.Password)) {
            return BadRequest("Login and password are required");
        }

        var actorScope = TenantBranchAuthorization.Resolve(HttpContext, _tenantContext, _configuration);
        await LegacyBranchBackfill.ApplyForConsoleAdminAsync(
            _db,
            actorScope,
            _tenantContext.BranchCode,
            _configuration,
            ct);

        var requestedTenantCode = TenantBranchAuthorization.NormalizeTenantCode(request.TenantCode);
        if (string.IsNullOrWhiteSpace(requestedTenantCode)) {
            return BadRequest("TenantCode is required");
        }

        if (!CanAccessTenant(actorScope, requestedTenantCode)) {
            return Forbid();
        }

        if (!ValidateBranchAssignments(actorScope, request.BranchRoles, request.RoleIds)) {
            return Forbid();
        }

        var login = request.Login.Trim().ToLowerInvariant();
        // Sadece aktif credential tekrar eklemeyi bloklar; pasif satir audit icin korunur.
        if (await _db.Credentials.AnyAsync(c => c.Login == login && c.IsActive, ct)) {
            return Conflict("Credential already exists");
        }

        var tenant = await _db.Tenants.FirstOrDefaultAsync(
            t => t.Code == requestedTenantCode || t.Slug == requestedTenantCode,
            ct);
        if (tenant == null) {
            return BadRequest("TenantCode is not valid");
        }

        var user = new TenantUser {
            TenantId = tenant.Id,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? login : request.DisplayName.Trim(),
            FirstName = request.FirstName?.Trim(),
            LastName = request.LastName?.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? login : request.Email.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            City = request.City?.Trim(),
            Country = request.Country?.Trim(),
            PositionTitle = request.PositionTitle?.Trim(),
            MarketingOptIn = request.MarketingOptIn
        };

        var credential = new Credential {
            Login = login,
            Email = user.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            OwnerType = CredentialOwnerType.TenantUser,
            TenantUser = user,
            IsActive = true,
            MustChangePassword = true
        };

        if (request.BranchRoles is { Length: > 0 }) {
            var roleIds = request.BranchRoles
                .Select(br => br.RoleId)
                .Where(id => Ulid.TryParse(id, out _))
                .Select(Ulid.Parse)
                .Distinct()
                .ToArray();
            var rolesMap = await _db.Roles
                .Where(r => roleIds.Contains(r.Id) && r.Scope == RoleScope.Tenant)
                .ToDictionaryAsync(r => r.Id, ct);

            foreach (var br in request.BranchRoles) {
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
        else if (request.RoleIds is { Length: > 0 }) {
            if (actorScope.RequiresBranchScope) {
                return Forbid();
            }

            var roleIds = request.RoleIds
                .Where(id => Ulid.TryParse(id, out _))
                .Select(Ulid.Parse)
                .ToArray();

            var roles = await _db.Roles.Where(r => roleIds.Contains(r.Id) && r.Scope == RoleScope.Tenant).ToListAsync(ct);
            foreach (var role in roles) {
                user.Roles.Add(new UserRole {
                    Role = role,
                    TenantUser = user
                });
            }
        }

        user.Credentials.Add(credential);
        _db.TenantUsers.Add(user);
        _db.Credentials.Add(credential);

        await _db.SaveChangesAsync(ct);

        return Created($"/auth/credentials/{credential.Id}", new {
            id = credential.Id.ToString(),
            credential.Login,
            user.DisplayName
        });
    }

    [HttpPatch("{id}/active")]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> SetActive([FromRoute] string id, [FromBody] SetActiveRequest request, CancellationToken ct = default) {
        if (!PermissionAuthorization.HasAnyPermission(HttpContext, _configuration, ManagePermissions)) {
            return Forbid();
        }

        if (!Ulid.TryParse(id, out var credentialId)) {
            return NotFound();
        }

        var credential = await _db.Credentials
            .Include(c => c.TenantUser)
                .ThenInclude(u => u!.Tenant)
            .Include(c => c.TenantUser)
                .ThenInclude(u => u!.Roles)
            .FirstOrDefaultAsync(
                c => c.Id == credentialId && (c.TenantUser == null || c.TenantUser.Status != TenantUserStatus.Deleted),
                ct);
        if (credential == null) {
            return NotFound();
        }

        var actorScope = TenantBranchAuthorization.Resolve(HttpContext, _tenantContext, _configuration);
        if (!CanManageCredential(actorScope, credential)) {
            return Forbid();
        }

        credential.IsActive = request.Active;
        await _db.SaveChangesAsync(ct);

        return Ok(new { id = credential.Id.ToString(), credential.IsActive });
    }

    private static bool CanAccessTenant(TenantBranchActorScope actorScope, string requestedTenantCode) {
        // Tenant-crossing decision: only platform actors bypass. TenantOwner must match their own
        // tenant (matched against the full code/slug identifier set).
        return TenantBranchAuthorization.IsTenantAllowed(actorScope, requestedTenantCode);
    }

    private static bool CanManageCredential(TenantBranchActorScope actorScope, Credential credential) {
        if (actorScope.IsPlatformBypass) {
            return true;
        }

        if (credential.TenantUser == null) {
            return false;
        }

        return TenantBranchAuthorization.IsTenantUserInScope(actorScope, credential.TenantUser);
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
            var branches = branchRoles.Select(role => role.BranchCode);
            var rejectEmptyCodes = actorScope.RequiresBranchScope || actorScope.BranchCodes.Count > 0;
            return TenantBranchAuthorization.AreBranchesAllowed(actorScope, branches, rejectEmptyCodes);
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
