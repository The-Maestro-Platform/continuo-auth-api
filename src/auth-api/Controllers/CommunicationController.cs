using AuthApi.Contracts.Requests;
using AuthApi.Infrastructure.Authorization;
using AuthApi.Models;
using AuthApi.Permissions;
using AuthApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace AuthApi.Controllers;

[ApiController]
[Route("communication")]
[AuthorizeUserType(UserType.PlatformUser, UserType.TenantUser)]
public class CommunicationController : ControllerBase {
    private readonly CommunicationService _communication;
    private readonly IConfiguration _configuration;

    private static readonly string[] PlatformUserManagePermissions = [
        PermissionKeys.Platform.AuthUsersManage
    ];

    private static readonly string[] TenantUserManagePermissions = [
        PermissionKeys.Tenant.BranchManage,
        PermissionKeys.Tenant.UsersManage
    ];

    public CommunicationController(CommunicationService communication, IConfiguration configuration) {
        _communication = communication;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] CommunicationOwnerType ownerType, [FromQuery] string ownerId, CancellationToken ct) {
        if (!Ulid.TryParse(ownerId, out var parsed)) {
            return BadRequest("Invalid ownerId");
        }

        var dto = await _communication.GetAsync(ownerType, parsed, ct);
        if (dto == null) {
            return NotFound();
        }

        return Ok(dto);
    }

    [HttpPut("addresses")]
    public async Task<IActionResult> UpsertAddresses([FromBody] UpsertAddressesRequest request, CancellationToken ct) {
        if (!Ulid.TryParse(request.OwnerId, out var ownerId)) {
            return BadRequest("Invalid ownerId");
        }

        if (!HasAccess(request.OwnerType)) {
            return Forbid();
        }

        var valid = request.Addresses?.All(a => !string.IsNullOrWhiteSpace(a.Line1)) ?? true;
        if (!valid) {
            return BadRequest("Line1 is required for all addresses");
        }

        var result = await _communication.UpsertAddressesAsync(request.OwnerType, ownerId, request.Addresses ?? Array.Empty<AddressDto>(), ct);
        if (result == null) {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPut("phones")]
    public async Task<IActionResult> UpsertPhones([FromBody] UpsertPhonesRequest request, CancellationToken ct) {
        if (!Ulid.TryParse(request.OwnerId, out var ownerId)) {
            return BadRequest("Invalid ownerId");
        }

        if (!HasAccess(request.OwnerType)) {
            return Forbid();
        }

        var valid = request.Phones?.All(p => !string.IsNullOrWhiteSpace(p.Number)) ?? true;
        if (!valid) {
            return BadRequest("Number is required for all phones");
        }

        var result = await _communication.UpsertPhonesAsync(request.OwnerType, ownerId, request.Phones ?? Array.Empty<PhoneDto>(), ct);
        if (result == null) {
            return NotFound();
        }

        return Ok(result);
    }

    private bool HasAccess(CommunicationOwnerType ownerType) {
        // Platform users manage platform users; tenant managers manage tenant branch staff.
        return ownerType switch {
            CommunicationOwnerType.PlatformUser =>
                PermissionAuthorization.HasAnyPermission(HttpContext, _configuration, PlatformUserManagePermissions),
            CommunicationOwnerType.TenantUser =>
                PermissionAuthorization.HasAnyPermission(HttpContext, _configuration, TenantUserManagePermissions),
            _ => false
        };
    }
}
