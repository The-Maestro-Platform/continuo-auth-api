using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AuthApi.Contracts.Requests;
using AuthApi.Infrastructure.Authorization;
using AuthApi.Models;
using AuthApi.Permissions;
using AuthApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Continuo.Configuration.Abstractions;
using Continuo.Shared.Security;

namespace AuthApi.Controllers;

[ApiController]
[Route("auth/users")]
[AuthorizeUserType(UserType.PlatformUser, UserType.TenantUser)]
public class UsersController : ControllerBase {
    private readonly AuthDbContext _db;
    private readonly IParameterProvider _parameters;
    private readonly UsersService _users;
    private readonly IConfiguration _configuration;
    private static readonly string[] ManagementRoles = { "company-owner", "operations-manager" };
    private static readonly string[] UserViewPermissions = [
        PermissionKeys.Platform.AuthUsersView,
        PermissionKeys.Platform.AuthUsersManage,
        PermissionKeys.Tenant.UsersView,
        PermissionKeys.Tenant.UsersManage,
        PermissionKeys.Tenant.BranchManage
    ];
    private static readonly string[] UserManagePermissions = [
        PermissionKeys.Platform.AuthUsersManage,
        PermissionKeys.Tenant.UsersManage,
        PermissionKeys.Tenant.BranchManage
    ];

    public UsersController(
        AuthDbContext db,
        IParameterProvider parameters,
        UsersService users,
        IConfiguration configuration) {
        _db = db;
        _parameters = parameters;
        _users = users;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? tenantCode, [FromQuery] int? take, CancellationToken ct) {
        if (!HasUserViewAccess()) {
            return Forbid();
        }

        var list = await _users.ListAsync(tenantCode, take, ct);
        return Ok(list);
    }

    // Env resolved centrally via Continuo.Shared.Env.EnvUtil

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser([FromRoute] string id, [FromBody] UpdateUserRequest request, CancellationToken ct) {
        if (!Ulid.TryParse(id, out var userId)) {
            return NotFound();
        }

        var actorUserId = await ResolveActorUserIdAsync();
        var isSelf = actorUserId.HasValue && actorUserId.Value == userId;
        if (!isSelf && !HasUserManageAccess()) {
            return Forbid();
        }

        var ok = await _users.UpdateUserAsync(userId, new UpdateUserData(
            request.DisplayName,
            request.FirstName,
            request.LastName,
            request.Email,
            request.PhoneNumber,
            request.AddressLine1,
            request.AddressLine2,
            request.City,
            request.Country,
            request.PostalCode,
            request.PositionTitle,
            request.MarketingOptIn
        ), isSelf, ct);

        if (!ok) {
            return NotFound();
        }

        return Ok(new { id });
    }

    private async Task<Ulid?> ResolveActorUserIdAsync() {
        var userClaim = HttpContext.User?.FindFirst("user_id")?.Value;
        if (!string.IsNullOrWhiteSpace(userClaim) && Ulid.TryParse(userClaim, out var parsedUserId)) {
            return parsedUserId;
        }

        var credentialClaim = HttpContext.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? HttpContext.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? HttpContext.User?.FindFirst("sub")?.Value;

        if (!string.IsNullOrWhiteSpace(credentialClaim) && Ulid.TryParse(credentialClaim, out var credentialId)) {
            var credential = await _db.Credentials.AsNoTracking().FirstOrDefaultAsync(c => c.Id == credentialId);
            if (credential != null && credential.TenantUserId.HasValue) {
                return credential.TenantUserId.Value;
            }
        }

        return null;
    }

    private bool HasUserViewAccess() {
        return ClaimsHelper.HasAnyRole(HttpContext, ManagementRoles)
               || PermissionAuthorization.HasAnyPermission(HttpContext, _configuration, UserViewPermissions);
    }

    private bool HasUserManageAccess() {
        return ClaimsHelper.HasAnyRole(HttpContext, ManagementRoles)
               || PermissionAuthorization.HasAnyPermission(HttpContext, _configuration, UserManagePermissions);
    }
}
