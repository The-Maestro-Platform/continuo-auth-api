using System.Linq;
using AuthApi.Contracts.Requests;
using AuthApi.Contracts.Responses;
using AuthApi.Infrastructure;
using AuthApi.Infrastructure.Authorization;
using AuthApi.Permissions;
using AuthApi.Models;
using AuthApi.Services;
using Microsoft.AspNetCore.Mvc;
using Continuo.Observability.Attributes;
using Continuo.Shared.Security;

namespace AuthApi.Controllers;

[ApiController]
[Route("auth/customers")]
public class CustomersController : ControllerBase {
    private readonly CustomersService _customers;
    private readonly AuthApi.Infrastructure.ITenantContext _tenantContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CustomersController> _logger;
    private static readonly PlatformRole[] ManagementRoles = {
        PlatformRole.PlatformOwner, PlatformRole.PlatformAdmin, PlatformRole.PlatformSupport
    };
    private static readonly PlatformRole[] CustomerManagementRoles = {
        PlatformRole.PlatformOwner, PlatformRole.PlatformAdmin, PlatformRole.PlatformSupport,
        PlatformRole.TenantOwner, PlatformRole.TenantAdmin, PlatformRole.OperationManager
    };
    private static readonly string[] CustomerViewPermissions = [
        PermissionKeys.Tenant.CustomersView,
        PermissionKeys.Tenant.CustomersManage,
        PermissionKeys.Tenant.OrdersView,
        PermissionKeys.Tenant.OrdersManage
    ];
    private static readonly string[] CustomerManagePermissions = [
        PermissionKeys.Tenant.CustomersManage,
        PermissionKeys.Tenant.OrdersManage
    ];

    public CustomersController(
        CustomersService customers,
        AuthApi.Infrastructure.ITenantContext tenantContext,
        IConfiguration configuration,
        ILogger<CustomersController> logger) {
        _customers = customers;
        _tenantContext = tenantContext;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct) {
        try {
            var (id, login) = await _customers.RegisterAsync(
                request.Email,
                request.Password,
                request.DisplayName,
                request.PhoneNumber,
                request.AddressLine1,
                request.AddressLine2,
                request.City,
                request.Country,
                request.PostalCode,
                request.TenantCode,
                request.MarketingOptIn,
                ct);

            return Created($"/auth/credentials/{id}", new { id = id.ToString(), login });
        }
        catch (InvalidOperationException ex) {
            return Conflict(ex.Message);
        }
        catch (ArgumentException ex) {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("list")]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default) {
        var hasRoleAccess = ClaimsHelper.HasAnyRole(HttpContext, CustomerManagementRoles);
        var hasPermissionAccess = PermissionAuthorization.HasAnyPermission(HttpContext, _configuration, CustomerViewPermissions);
        if (!hasRoleAccess && !hasPermissionAccess) {
            return Forbid();
        }

        if (!_tenantContext.HasTenant || _tenantContext.TenantId == null) {
            return BadRequest(new { message = "Tenant context required" });
        }

        var data = await _customers.ListByTenantAsync(_tenantContext.TenantId.Value, search, skip, take, ct);
        return Ok(data);
    }

    [HttpGet("profile/{login}")]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> GetProfile([FromRoute] string login) {
        if (string.IsNullOrWhiteSpace(login)) {
            return BadRequest("Login is required");
        }

        var normalized = login.Trim().ToLowerInvariant();
        if (!IsSelfOrViewer(normalized)) {
            return Forbid();
        }

        var credential = await _customers.GetCustomerCredentialAsync(normalized, HttpContext.RequestAborted);
        if (credential == null || credential.Customer == null) {
            return NotFound();
        }

        return Ok(ToResponse(credential));
    }

    [HttpPut("profile/{login}")]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> UpdateProfile([FromRoute] string login, [FromBody] UpdateCustomerProfileRequest request, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(login)) {
            return BadRequest("Login is required");
        }

        var normalized = login.Trim().ToLowerInvariant();
        if (!IsSelfOrManager(normalized)) {
            return Forbid();
        }

        var updated = await _customers.UpdateProfileAsync(normalized, new UpdateProfileData(
            request.DisplayName,
            request.FullName,
            request.PhoneNumber,
            request.AddressLine1,
            request.AddressLine2,
            request.City,
            request.Country,
            request.PostalCode,
            request.MarketingOptIn
        ), ct);

        if (updated == null) {
            return NotFound();
        }

        return Ok(ToResponse(updated));
    }

    [HttpPost("{login}/agreements")]
    [HttpPost("agreements")]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> AcceptAgreements([FromRoute] string? login, [FromBody] AcceptAgreementsRequest request, CancellationToken ct) {
        var resolvedLogin = login;
        if (string.IsNullOrWhiteSpace(resolvedLogin)) {
            resolvedLogin = request.Login;
        }

        if (string.IsNullOrWhiteSpace(resolvedLogin)) {
            return BadRequest("Login is required");
        }

        var normalized = resolvedLogin!.Trim().ToLowerInvariant();
        if (!IsSelfOrManager(normalized)) {
            return Forbid();
        }

        // 2026-05-15: Platform user (PlatformOwner/Admin/Support) **müşteri** olarak QR menü
        // signup yapamaz; tenant user'lar (TenantOwner, TenantAdmin, OperationManager vb.) izin.
        // Platform user'lar Identity tarafında platform-scope kayıtlı, Customer tablosunda yok →
        // mevcut akışta null Customer dönüp 404 alıyorlardı; net mesajla 409 dönelim.
        if (ClaimsHelper.HasAnyRole(HttpContext, ManagementRoles)) {
            return Conflict(new {
                code = "platform-user-not-allowed",
                message = "Platform / yönetim hesapları müşteri olarak QR menü kullanamaz. Normal müşteri hesabıyla giriş yapın."
            });
        }

        var cred = await _customers.AcceptAgreementsAsync(normalized, request.Version, request.MarketingOptIn, ct);
        if (cred == null) {
            // Tenant user ama Customer kaydı henüz yok → signup flow eksik. Yapılandırılmış
            // hata: UI önce customer-create endpoint'i çağırmalı veya AgreementsModal'ı bu
            // durumda göstermemelidir.
            return NotFound(new {
                code = "customer-not-found",
                message = "Müşteri kaydınız bulunamadı. Lütfen tekrar kayıt olmayı deneyin."
            });
        }

        return Ok(ToResponse(cred));
    }

    [HttpPatch("{id}/active")]
    [ContinuoProxyMethod("ui")]
    public async Task<IActionResult> SetActive([FromRoute] string id, [FromBody] SetActiveRequest request, CancellationToken ct) {
        if (!PermissionAuthorization.HasAnyPermission(HttpContext, _configuration, CustomerManagePermissions)
            && !ClaimsHelper.HasAnyRole(HttpContext, CustomerManagementRoles)) {
            return Forbid();
        }

        if (!_tenantContext.HasTenant || _tenantContext.TenantId == null) {
            return BadRequest(new { message = "Tenant context required" });
        }

        if (!Ulid.TryParse(id, out var customerId)) {
            return NotFound();
        }

        var result = await _customers.SetActiveAsync(_tenantContext.TenantId.Value, customerId, request.Active, ct);
        if (result == null) {
            return NotFound();
        }

        return Ok(new { id, isActive = result.Value });
    }

    [HttpPost("summaries")]
    [ContinuoProxyMethod("ui")]
    [ContinuoAppProxyMethod("auth.customers.summaries")]
    public async Task<IActionResult> Summaries([FromBody] CustomerSummariesRequest request, CancellationToken ct) {
        if (!PermissionAuthorization.HasAnyPermission(HttpContext, _configuration, CustomerViewPermissions)) {
            return Forbid();
        }

        var ids = request.CustomerIds ?? Array.Empty<string>();
        var parsed = new List<Ulid>(ids.Length);
        foreach (var id in ids) {
            if (string.IsNullOrWhiteSpace(id)) {
                continue;
            }
            if (Ulid.TryParse(id, out var parsedId)) {
                parsed.Add(parsedId);
            }
        }

        var data = await _customers.GetCustomerSummariesAsync(parsed, ct);
        return Ok(data);
    }

    private static CustomerProfileResponse ToResponse(Credential credential) {
        var customer = credential.Customer!;
        return new CustomerProfileResponse(
            customer.Id.ToString(),
            credential.Login,
            customer.DisplayName ?? credential.Login,
            customer.Email ?? credential.Customer?.Email,
            customer.PhoneNumber,
            customer.AddressLine1,
            customer.AddressLine2,
            customer.City,
            customer.Country,
            customer.PostalCode,
            customer.MarketingOptIn,
            credential.AgreementsAcceptedAtUtc.HasValue,
            credential.AgreementsAcceptedAtUtc,
            new TenantSummary(customer.TenantId.ToString(), customer.Tenant.Code, customer.Tenant.Name));
    }

    private bool IsSelfOrManager(string normalizedLogin) {
        var actorLogin = HttpContext.User?.FindFirst("login")?.Value;
        if (!string.IsNullOrWhiteSpace(actorLogin) &&
            string.Equals(actorLogin, normalizedLogin, StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        if (ClaimsHelper.HasAnyRole(HttpContext, ManagementRoles)) {
            return true;
        }

        if (PermissionAuthorization.HasAnyPermission(HttpContext, _configuration, CustomerManagePermissions)) {
            return true;
        }

        LogAuthorizationDenied(nameof(IsSelfOrManager), normalizedLogin, actorLogin);
        return false;
    }

    private bool IsSelfOrViewer(string normalizedLogin) {
        var actorLogin = HttpContext.User?.FindFirst("login")?.Value;
        if (!string.IsNullOrWhiteSpace(actorLogin) &&
            string.Equals(actorLogin, normalizedLogin, StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        if (ClaimsHelper.HasAnyRole(HttpContext, CustomerManagementRoles)) {
            return true;
        }

        if (PermissionAuthorization.HasAnyPermission(HttpContext, _configuration, CustomerViewPermissions)) {
            return true;
        }

        LogAuthorizationDenied(nameof(IsSelfOrViewer), normalizedLogin, actorLogin);
        return false;
    }

    private void LogAuthorizationDenied(string gate, string requestedLogin, string? actorLogin) {
        // 2026-05-18 staging 403 incident — kim, neyi, hangi claim ile istedi tek satırda.
        // Bootstrap.cs `Microsoft.AspNetCore`'u Warning'e zorluyor; bu log app-level
        // category olduğu için Information seviyesinde görünür.
        var hasAnyClaim = HttpContext.User?.Identity?.IsAuthenticated == true;
        var roles = HttpContext.User?.FindAll(System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value).ToArray() ?? Array.Empty<string>();
        var tenant = HttpContext.User?.FindFirst("tenant_code")?.Value;
        _logger.LogInformation(
            "Customer authorization denied — gate={Gate} requested={RequestedLogin} actor={ActorLogin} authenticated={Auth} roles=[{Roles}] tenant={Tenant} path={Path}",
            gate,
            requestedLogin,
            string.IsNullOrWhiteSpace(actorLogin) ? "(anonymous)" : actorLogin,
            hasAnyClaim,
            string.Join(",", roles),
            tenant ?? "(none)",
            HttpContext.Request.Path);
    }
}
