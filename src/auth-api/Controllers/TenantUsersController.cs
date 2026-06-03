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

        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Code == tenantCode || t.Slug == tenantCode, ct);
        if (tenant == null) {
            return BadRequest("TenantCode is not valid");
        }

        // Ayni tenant'ta aktif TenantUser varsa gercek conflict.
        if (await _db.TenantUsers.AnyAsync(
                u => u.TenantId == tenant.Id && u.Email == email && u.Status == TenantUserStatus.Active, ct)) {
            return Conflict("User already exists in this tenant");
        }

        // Pasif TenantUser ayni tenant'ta varsa: yeni satir acma — eskisini reaktive et.
        var inactiveUser = await _db.TenantUsers
            .Include(u => u.Credentials)
            .Where(u => u.TenantId == tenant.Id
                && u.Email == email
                && u.Status != TenantUserStatus.Active
                && u.Status != TenantUserStatus.Deleted)
            .OrderByDescending(u => u.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        TenantUser user;
        Credential credential;
        var attachedToExisting = false;
        var passwordIgnored = false;
        var reactivated = false;

        if (inactiveUser != null) {
            inactiveUser.Status = TenantUserStatus.Active;
            inactiveUser.DisplayName = displayName;
            inactiveUser.UpdatedAtUtc = DateTime.UtcNow;

            var ownCredential = inactiveUser.Credentials
                .OrderByDescending(c => c.CreatedAtUtc)
                .FirstOrDefault(c => c.Login == email);

            if (ownCredential != null) {
                ownCredential.IsActive = true;
                ownCredential.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
                ownCredential.MustChangePassword = true;
                ownCredential.PasswordChangedAtUtc = null;
                credential = ownCredential;
            }
            else {
                credential = new Credential {
                    Login = email,
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
                    OwnerType = CredentialOwnerType.TenantUser,
                    TenantUser = inactiveUser,
                    IsActive = true,
                    MustChangePassword = true
                };
                inactiveUser.Credentials.Add(credential);
                _db.Credentials.Add(credential);
            }

            user = inactiveUser;
            reactivated = true;
        }
        else {
            // Ayni mailde aktif credential olabilir (PlatformUser veya Customer'dan).
            // Bu durumda yeni credential acmak yerine mevcut credential'a TenantUser FK'sini
            // iliştir: tek hesap, ortak parola, iki taraflı yetki. Parola dokunulmaz —
            // kullanici mevcut sifresiyle login olmaya devam eder; admin yazdigi sifre ignore.
            var existingActiveCredential = await _db.Credentials
                .FirstOrDefaultAsync(c => c.Login == email && c.IsActive, ct);

            if (existingActiveCredential != null) {
                if (existingActiveCredential.TenantUserId.HasValue) {
                    // Credential zaten baska bir TenantUser'a (baska tenant) bagli.
                    // Tek FK kolonu var; iki tenant'a ayni anda baglanamaz.
                    var otherTenantUser = await _db.TenantUsers
                        .Where(u => u.Id == existingActiveCredential.TenantUserId.Value)
                        .Select(u => new { u.TenantId, u.Status })
                        .FirstOrDefaultAsync(ct);

                    if (otherTenantUser != null
                            && otherTenantUser.TenantId != tenant.Id
                            && otherTenantUser.Status != TenantUserStatus.Deleted) {
                        return Conflict(
                            "Bu email baska bir isletmede zaten kullaniliyor. Ayni kimligi birden fazla isletmeye baglamak su an desteklenmiyor.");
                    }
                }

                user = new TenantUser {
                    TenantId = tenant.Id,
                    DisplayName = displayName,
                    Email = email,
                    Status = TenantUserStatus.Active
                };
                _db.TenantUsers.Add(user);

                existingActiveCredential.TenantUser = user;
                // Platform > Tenant > Customer onceligi: Platform credential ise OwnerType degisme.
                // Customer credential ise TenantUser'a yukselt (TenantUser > Customer).
                if (existingActiveCredential.OwnerType == CredentialOwnerType.Customer) {
                    existingActiveCredential.OwnerType = CredentialOwnerType.TenantUser;
                }

                credential = existingActiveCredential;
                attachedToExisting = true;
                passwordIgnored = true;
            }
            else {
                user = new TenantUser {
                    TenantId = tenant.Id,
                    DisplayName = displayName,
                    Email = email,
                    Status = TenantUserStatus.Active
                };

                credential = new Credential {
                    Login = email,
                    Email = email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
                    OwnerType = CredentialOwnerType.TenantUser,
                    TenantUser = user,
                    IsActive = true
                };

                user.Credentials.Add(credential);
                _db.TenantUsers.Add(user);
                _db.Credentials.Add(credential);
            }
        }

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

        await _db.SaveChangesAsync(ct);

        return Created($"/auth/tenant-users/{user.Id}", new {
            id = user.Id.ToString(),
            user.Email,
            user.DisplayName,
            attachedToExistingCredential = attachedToExisting,
            passwordIgnored,
            reactivated
        });
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
        // Multi-membership: bir credential ayni anda hem PlatformUser hem TenantUser'a
        // (veya Customer'a) bagli olabilir. Bu TenantUser'i disable etmek shared
        // credential'in IsActive'ini false yaparsa platform/customer login'i de kirilir
        // — gizli baska-membership kapama. credential.IsActive sadece bu TenantUser
        // dis kalan tek owner ise degistirilmeli; aksi halde TenantUser.Status alone
        // taşıyor.
        foreach (var cred in user.Credentials) {
            var isExclusivelyTenant = cred.PlatformUserId == null && cred.CustomerId == null;
            if (isExclusivelyTenant) {
                cred.IsActive = req.Active;
            }
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

        // Multi-membership: shared credential'i IsActive=false yapma — Platform/Customer
        // login'i de kapanir. Shared olanlar icin sadece TenantUserId FK'sini sok
        // (credential aktif kaliyor, baska-membership login'i devam ediyor). Tek-owner
        // olanlar icin eski davranis: credential'i kapatup.
        foreach (var credential in user.Credentials) {
            var isExclusivelyTenant = credential.PlatformUserId == null && credential.CustomerId == null;
            if (isExclusivelyTenant) {
                credential.IsActive = false;
            }
            else {
                // CHECK constraint (AuthDbContext: en az 1 owner) baska FK kalmasi
                // sayesinde holding; sadece tenant-tarafini cozuyoruz.
                credential.TenantUserId = null;
                credential.TenantUser = null;
            }
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
