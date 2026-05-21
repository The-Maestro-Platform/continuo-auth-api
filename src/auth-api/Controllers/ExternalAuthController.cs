using AuthApi.Contracts.Requests;
using AuthApi.Models;
using AuthApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Continuo.Observability.Attributes;

namespace AuthApi.Controllers;

[ApiController]
[Route("auth/external")]
public class ExternalAuthController : ControllerBase {
    private static readonly HashSet<string> CustomerFacingApps = new(StringComparer.OrdinalIgnoreCase) {
        "qrmenu-web",
        "qrmenu-mobile",
        "public-web",
        "continuo-web"
    };

    private readonly AuthDbContext _db;
    private readonly IGoogleAuthService _googleAuthService;
    private readonly ITokenService _tokenService;
    private readonly TwoFactorService _twoFactorService;
    private readonly IScreenAccessService _screenAccess;
    private readonly IConfiguration _config;
    private readonly ILogger<ExternalAuthController> _logger;

    public ExternalAuthController(
        AuthDbContext db,
        IGoogleAuthService googleAuthService,
        ITokenService tokenService,
        TwoFactorService twoFactorService,
        IScreenAccessService screenAccess,
        IConfiguration config,
        ILogger<ExternalAuthController> logger) {
        _db = db;
        _googleAuthService = googleAuthService;
        _tokenService = tokenService;
        _twoFactorService = twoFactorService;
        _screenAccess = screenAccess;
        _config = config;
        _logger = logger;
    }

    [ContinuoProxyMethod("ui")]
    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin([FromBody] ExternalGoogleLoginRequest req) {
        if (string.IsNullOrWhiteSpace(req.IdToken)) {
            return BadRequest(new { message = "IdTokenRequired" });
        }

        // Feature flag kontrolu
        var googleLoginEnabled = _config.GetValue("Integrations:Auth:GoogleLoginEnabled", true);
        if (!googleLoginEnabled) {
            return BadRequest(new { message = "GoogleLoginDisabled" });
        }

        // Google token dogrulama
        var googleUser = await _googleAuthService.ValidateIdTokenAsync(req.IdToken, HttpContext.RequestAborted);
        if (googleUser == null) {
            await Task.Delay(Random.Shared.Next(50, 200));
            return Unauthorized(new { message = "InvalidGoogleToken" });
        }

        if (string.IsNullOrWhiteSpace(googleUser.Email)) {
            return Unauthorized(new { message = "GoogleTokenMissingEmail" });
        }

        // ExternalLogin tablosunda ara
        var externalLogin = await _db.ExternalLogins
            .Include(e => e.Credential)
                .ThenInclude(c => c!.PlatformUser)
                    .ThenInclude(u => u!.Roles)
                        .ThenInclude(r => r.Role!)
                            .ThenInclude(role => role!.Permissions)
            .Include(e => e.Credential)
                .ThenInclude(c => c!.TenantUser)
                    .ThenInclude(u => u!.Tenant)
            .Include(e => e.Credential)
                .ThenInclude(c => c!.TenantUser)
                    .ThenInclude(u => u!.Roles)
                        .ThenInclude(r => r.Role!)
                            .ThenInclude(role => role!.Permissions)
            .Include(e => e.Credential)
                .ThenInclude(c => c!.Customer)
                    .ThenInclude(cust => cust!.Tenant)
            .FirstOrDefaultAsync(e => e.Provider == "Google" && e.ProviderUserId == googleUser.Sub, HttpContext.RequestAborted);

        Credential? credential = null;

        if (externalLogin != null) {
            // Mevcut external login
            credential = externalLogin.Credential;
            externalLogin.LastUsedAtUtc = DateTime.UtcNow;
            externalLogin.ProviderDisplayName = googleUser.Name;
            externalLogin.ProfilePictureUrl = googleUser.Picture;
        } else {
            // Email ile mevcut credential ara
            credential = await FindCredentialByEmailAsync(googleUser.Email, HttpContext.RequestAborted);

            if (credential != null) {
                // Mevcut hesabi Google ile bagla
                var newExternalLogin = new ExternalLogin {
                    CredentialId = credential.Id,
                    Provider = "Google",
                    ProviderUserId = googleUser.Sub,
                    ProviderEmail = googleUser.Email,
                    ProviderDisplayName = googleUser.Name,
                    ProfilePictureUrl = googleUser.Picture,
                    LastUsedAtUtc = DateTime.UtcNow
                };
                _db.ExternalLogins.Add(newExternalLogin);

                _logger.LogInformation("Linked Google account to existing credential {CredentialId}", credential.Id);
            } else if (req.AutoRegister && !string.IsNullOrWhiteSpace(req.TenantCode)) {
                // Yeni customer olarak kaydet
                var tenant = await _db.Tenants.FirstOrDefaultAsync(
                    t => t.Code == req.TenantCode || t.Slug == req.TenantCode, HttpContext.RequestAborted);
                if (tenant == null) {
                    return BadRequest(new { message = "InvalidTenantCode" });
                }

                var customer = new Customer {
                    TenantId = tenant.Id,
                    Tenant = tenant,
                    Email = googleUser.Email,
                    DisplayName = googleUser.Name ?? googleUser.Email.Split('@')[0],
                    CreatedAtUtc = DateTime.UtcNow
                };
                _db.Customers.Add(customer);

                credential = new Credential {
                    Login = googleUser.Email,
                    Email = googleUser.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                    OwnerType = CredentialOwnerType.Customer,
                    Customer = customer,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow
                };
                _db.Credentials.Add(credential);

                var newExternalLogin = new ExternalLogin {
                    Credential = credential,
                    Provider = "Google",
                    ProviderUserId = googleUser.Sub,
                    ProviderEmail = googleUser.Email,
                    ProviderDisplayName = googleUser.Name,
                    ProfilePictureUrl = googleUser.Picture,
                    LastUsedAtUtc = DateTime.UtcNow
                };
                _db.ExternalLogins.Add(newExternalLogin);
                _db.OutboxMessages.Add(CustomerOutboxFactory.Registered(customer, tenant.Code));

                _logger.LogInformation(
                    "Created new customer via Google login. CustomerId: {CustomerId}, Tenant: {TenantCode}",
                    customer.Id,
                    tenant.Code);
            } else {
                return NotFound(new { message = "UserNotFound" });
            }
        }

        // Customer-facing app (AutoRegister + TenantCode): ensure a Customer record is linked
        // When a PlatformUser/TenantUser logs in from a customer app, link a Customer record
        // to the existing credential for customer-specific endpoints (profile, agreements, etc.)
        if (credential != null && credential.IsActive
            && credential.CustomerId == null
            && req.AutoRegister && !string.IsNullOrWhiteSpace(req.TenantCode)) {

            var tenant = await _db.Tenants.FirstOrDefaultAsync(
                t => t.Code == req.TenantCode || t.Slug == req.TenantCode, HttpContext.RequestAborted);
            if (tenant == null) {
                return BadRequest(new { message = "InvalidTenantCode" });
            }

            var customer = new Customer {
                TenantId = tenant.Id,
                Tenant = tenant,
                Email = googleUser.Email,
                DisplayName = googleUser.Name ?? googleUser.Email.Split('@')[0],
                CreatedAtUtc = DateTime.UtcNow
            };
            _db.Customers.Add(customer);
            credential.Customer = customer;
            _db.OutboxMessages.Add(CustomerOutboxFactory.Registered(customer, tenant.Code));

            _logger.LogInformation(
                "Linked Customer record to existing {OwnerType} credential. CustomerId: {CustomerId}, Tenant: {TenantCode}",
                credential.OwnerType,
                customer.Id,
                tenant.Code);
        }

        if (credential == null || !credential.IsActive) {
            return Unauthorized(new { message = "AccountInactive" });
        }

        if (!IsCustomerAllowedForClientApp(credential, ResolveClientApp())) {
            return Unauthorized(new { message = "Invalid credentials" });
        }

        await _db.SaveChangesAsync(HttpContext.RequestAborted);

        // Credential'i tam olarak yukle (kayit sonrasi)
        if (credential.PlatformUser == null && credential.TenantUser == null && credential.Customer == null) {
            credential = await FindCredentialByIdAsync(credential.Id, HttpContext.RequestAborted);
            if (credential == null) {
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "CredentialLoadError" });
            }
        }

        if (_twoFactorService.RequiresTwoFactor(credential)) {
            var challenge = await _twoFactorService.CreateChallengeAsync(credential, HttpContext.RequestAborted);
            return Ok(new {
                requiresTwoFactor = true,
                challengeId = challenge.Id.ToString(),
                expiresAtUtc = challenge.ExpiresAtUtc,
                channel = challenge.Channel,
                targetHint = MaskTarget(challenge.Target)
            });
        }

        credential.LastLoginAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(HttpContext.RequestAborted);

        var authResponse = await BuildAuthResponseAsync(credential);
        return Ok(authResponse);
    }

    [ContinuoProxyMethod("ui")]
    [HttpGet("providers")]
    public IActionResult GetEnabledProviders() {
        var providers = new List<string>();

        if (_config.GetValue("Integrations:Auth:GoogleLoginEnabled", true)) {
            providers.Add("google");
        }

        return Ok(new { providers });
    }

    private async Task<Credential?> FindCredentialByEmailAsync(string email, CancellationToken ct) {
        // Sadece aktif credential ile eslestir; pasif kayit Google OAuth handoff'unu
        // yanlis hesaba baglamasin.
        return await _db.Credentials
            .Include(c => c.PlatformUser)
                .ThenInclude(u => u!.Roles)
                    .ThenInclude(r => r.Role!)
                        .ThenInclude(role => role!.Permissions)
            .Include(c => c.TenantUser)
                .ThenInclude(u => u!.Tenant)
            .Include(c => c.TenantUser)
                .ThenInclude(u => u!.Roles)
                    .ThenInclude(r => r.Role!)
                        .ThenInclude(role => role!.Permissions)
            .Include(c => c.Customer)
                .ThenInclude(cust => cust!.Tenant)
            .FirstOrDefaultAsync(c => c.IsActive && c.Email == email, ct);
    }

    private string? ResolveClientApp() {
        var clientApp = HttpContext.Request.Query["app"].FirstOrDefault()?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(clientApp)) {
            clientApp = HttpContext.Request.Headers["X-Client-App"].FirstOrDefault()?.Trim().ToLowerInvariant();
        }
        return string.IsNullOrEmpty(clientApp) ? null : clientApp;
    }

    private static bool IsCustomerAllowedForClientApp(Credential credential, string? clientApp) =>
        credential.OwnerType != CredentialOwnerType.Customer
        || (!string.IsNullOrEmpty(clientApp) && CustomerFacingApps.Contains(clientApp));

    private async Task<Credential?> FindCredentialByIdAsync(Ulid id, CancellationToken ct) {
        return await _db.Credentials
            .Include(c => c.PlatformUser)
                .ThenInclude(u => u!.Roles)
                    .ThenInclude(r => r.Role!)
                        .ThenInclude(role => role!.Permissions)
            .Include(c => c.TenantUser)
                .ThenInclude(u => u!.Tenant)
            .Include(c => c.TenantUser)
                .ThenInclude(u => u!.Roles)
                    .ThenInclude(r => r.Role!)
                        .ThenInclude(role => role!.Permissions)
            .Include(c => c.Customer)
                .ThenInclude(cust => cust!.Tenant)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    private async Task<object> BuildAuthResponseAsync(Credential credential) {
        var ownerRoles = ResolveRoles(credential).ToList();
        var roleNames = ownerRoles.Select(r => r.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var permissionKeys = ownerRoles.SelectMany(r => r.Permissions.Select(p => p.PermissionKey))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var appCode = HttpContext.Request.Query["app"].FirstOrDefault();
        var screens = await _screenAccess.ResolveScreensAsync(credential, permissionKeys, ownerRoles, appCode, HttpContext.RequestAborted);

        var (accessToken, accessExpires) = await _tokenService.CreateAccessTokenAsync(credential, roleNames, permissionKeys, screens);
        var (refreshToken, refreshExpires) = await _tokenService.CreateRefreshTokenAsync();

        return new {
            accessToken,
            accessExpires,
            refreshToken,
            refreshExpires,
            credential = new {
                id = credential.Id.ToString(),
                customerId = credential.CustomerId?.ToString(),
                login = credential.Login,
                displayName = ResolveDisplayName(credential),
                email = credential.Email,
                ownerType = credential.OwnerType,
                roles = roleNames,
                permissions = permissionKeys,
                screens,
                tenant = ResolveTenantSummary(credential),
                agreementsAccepted = credential.AgreementsAcceptedAtUtc.HasValue,
                agreementsAcceptedAtUtc = credential.AgreementsAcceptedAtUtc,
                mustChangePassword = credential.MustChangePassword
            }
        };
    }

    private static string ResolveDisplayName(Credential credential) {
        return credential.OwnerType switch {
            CredentialOwnerType.PlatformUser => credential.PlatformUser?.DisplayName ?? credential.Login,
            CredentialOwnerType.TenantUser => credential.TenantUser?.DisplayName ?? credential.Login,
            CredentialOwnerType.Customer => credential.Customer?.DisplayName ?? credential.Login,
            _ => credential.Login
        };
    }

    private static object? ResolveTenantSummary(Credential credential) {
        if (credential.OwnerType == CredentialOwnerType.TenantUser && credential.TenantUser?.Tenant != null) {
            return new { id = credential.TenantUser.TenantId.ToString(), credential.TenantUser.Tenant.Code, credential.TenantUser.Tenant.Name };
        }

        if (credential.Customer?.Tenant != null) {
            return new { id = credential.Customer.TenantId.ToString(), credential.Customer.Tenant.Code, credential.Customer.Tenant.Name };
        }

        return null;
    }

    private static string MaskTarget(string target) {
        if (string.IsNullOrWhiteSpace(target)) {
            return string.Empty;
        }

        var atIdx = target.IndexOf('@');
        if (atIdx > 0) {
            var local = target.Substring(0, atIdx);
            var domain = target.Substring(atIdx);
            if (local.Length <= 2) {
                return new string('*', local.Length) + domain;
            }
            return $"{local[..2]}***{domain}";
        }

        if (target.Length <= 4) {
            return "****";
        }
        return $"{target[..2]}***{target[^1]}";
    }

    private static IEnumerable<Role> ResolveRoles(Credential credential) {
        return credential.OwnerType switch {
            CredentialOwnerType.PlatformUser => credential.PlatformUser?.Roles.Select(r => r.Role) ?? Enumerable.Empty<Role>(),
            CredentialOwnerType.TenantUser => credential.TenantUser?.Roles.Select(r => r.Role) ?? Enumerable.Empty<Role>(),
            _ => Enumerable.Empty<Role>()
        };
    }
}
