using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AuthApi.Models;
using Microsoft.IdentityModel.Tokens;

namespace AuthApi.Services;

public class TokenService : ITokenService {
    private readonly IConfiguration _config;
    private readonly ILogger<TokenService> _logger;

    // 2026-05-20: Browser cookie value limiti RFC 6265 + most browsers = 4096 byte.
    // JWT bu sınıra yaklaşırsa veya aşarsa Set-Cookie response sessizce reddedilir →
    // sonsuz login loop. BFF cookie set öncesi guard'a sahip ama auth-api'nin de
    // operatör seviyesinde structured log'a düşmesi gerekiyor (Loki üzerinden
    // alarm kurulabilir).
    private const int CookieValueLimitBytes = 4096;
    private const int CookieValueWarnBytes = 3800;

    public TokenService(IConfiguration config, ILogger<TokenService> logger) {
        _config = config;
        _logger = logger;
    }

    public Task<(string token, DateTime expires)> CreateRefreshTokenAsync() {
        var expires = DateTime.UtcNow.AddDays(30);
        var random = new byte[64];
        RandomNumberGenerator.Fill(random);
        var token = Convert.ToBase64String(random);
        return Task.FromResult((token, expires));
    }

    // 2026-05-20: `screens` parameter retained for source compatibility ama JWT'ye
    // claim olarak EKLENMİYOR — cookie 4KB sınırına çarpıyordu (incident: mert.cengiz
    // login loop). Screen listesi artık `/auth/claims` endpoint'inden DB-fresh
    // dönüyor; BFF onu cookie'ye populate ediyor. JWT minimum kimlik + role + tenant
    // taşır, permission/screen authoritative kaynak DB.
    public Task<(string token, DateTime expires)> CreateAccessTokenAsync(Credential credential, IEnumerable<string> roles, IEnumerable<string> permissions, IEnumerable<string> screens, IEnumerable<string>? branchCodes = null, IEnumerable<string>? branchRoles = null, Ulid? sessionId = null) {
        var jwtSecret = _config["JWT:SECRET"] ?? _config["JWT__SECRET"] ?? throw new InvalidOperationException("JWT secret not configured");
        var issuer = _config["JWT:ISSUER"] ?? _config["JWT__ISSUER"];
        var audience = _config["JWT:AUDIENCE"] ?? _config["JWT__AUDIENCE"];

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(15);

        var (displayName, phone, city, country, tenantId, tenantCode, tenantSlug, tenantName, ownerId) = ResolveOwnerContext(credential);

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, credential.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, credential.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, credential.Email ?? credential.Login),
            new Claim("name", displayName),
            new Claim("display_name", displayName),
            new Claim("login", credential.Login),
            new Claim("user_type", credential.OwnerType.ToString()),
            new Claim("user_id", ownerId ?? credential.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (sessionId.HasValue) {
            claims.Add(new Claim("sid", sessionId.Value.ToString()));
        }

        if (credential.MustChangePassword) {
            claims.Add(new Claim("must_change_password", "1"));
        }

        if (!string.IsNullOrWhiteSpace(phone)) {
            claims.Add(new Claim("phone_number", phone));
        }

        if (!string.IsNullOrWhiteSpace(city)) {
            claims.Add(new Claim("city", city));
        }

        if (!string.IsNullOrWhiteSpace(country)) {
            claims.Add(new Claim("country", country));
        }

        if (tenantId != null) {
            claims.Add(new Claim("tenant_id", tenantId));
        }

        if (!string.IsNullOrWhiteSpace(tenantCode)) {
            claims.Add(new Claim("tenant_code", tenantCode));
        }

        if (!string.IsNullOrWhiteSpace(tenantSlug)) {
            claims.Add(new Claim("tenant_slug", tenantSlug));
        }

        if (!string.IsNullOrWhiteSpace(tenantName)) {
            claims.Add(new Claim("tenant_name", tenantName));
        }

        foreach (var role in roles.Distinct(StringComparer.OrdinalIgnoreCase)) {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        foreach (var permission in permissions.Distinct(StringComparer.OrdinalIgnoreCase)) {
            claims.Add(new Claim("permission", permission));
        }

        // `screen` claim'leri JWT'ye eklenmez — cookie 4KB limitini aşıyordu.
        // UI middleware `/auth/claims`'ten gelen `admin_screens` cookie'sini kullanır.
        _ = screens; // suppress unused

        if (branchCodes != null) {
            foreach (var branch in branchCodes.Distinct(StringComparer.OrdinalIgnoreCase)) {
                claims.Add(new Claim("branch_code", branch));
            }
        }

        if (branchRoles != null) {
            foreach (var br in branchRoles.Distinct(StringComparer.OrdinalIgnoreCase)) {
                claims.Add(new Claim("branch_role", br));
            }
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        var written = new JwtSecurityTokenHandler().WriteToken(token);
        var sizeBytes = Encoding.UTF8.GetByteCount(written);
        if (sizeBytes > CookieValueLimitBytes) {
            _logger.LogError(
                "JWT token {Size} byte üretildi, browser cookie limiti {Limit} byte aşıldı. " +
                "Kullanıcı={Login} permissionCount={PermCount} screenCount={ScreenCount} " +
                "branchCount={BranchCount}. Set-Cookie sessizce reddedilecek, login loop ihtimali.",
                sizeBytes, CookieValueLimitBytes, credential.Login,
                permissions.Count(), screens.Count(), branchCodes?.Count() ?? 0);
        }
        else if (sizeBytes > CookieValueWarnBytes) {
            _logger.LogWarning(
                "JWT token {Size} byte — cookie limiti {Limit} byte'a yaklaşıyor (kalan buffer {Buffer} byte). " +
                "Kullanıcı={Login} permissionCount={PermCount} screenCount={ScreenCount}.",
                sizeBytes, CookieValueLimitBytes, CookieValueLimitBytes - sizeBytes,
                credential.Login, permissions.Count(), screens.Count());
        }
        return Task.FromResult((written, expires));
    }

    private static (string displayName, string? phone, string? city, string? country, string? tenantId, string? tenantCode, string? tenantSlug, string? tenantName, string ownerId) ResolveOwnerContext(Credential credential) {
        string displayName = credential.Login;
        string? phone = null;
        string? city = null;
        string? country = null;
        string? tenantId = null;
        string? tenantCode = null;
        string? tenantSlug = null;
        string? tenantName = null;
        string ownerId = credential.Id.ToString();

        if (credential.OwnerType == CredentialOwnerType.PlatformUser && credential.PlatformUser != null) {
            displayName = credential.PlatformUser.DisplayName;
            ownerId = credential.PlatformUser.Id.ToString();
        }
        else if (credential.OwnerType == CredentialOwnerType.TenantUser && credential.TenantUser != null) {
            var user = credential.TenantUser;
            displayName = user.DisplayName;
            ownerId = user.Id.ToString();
            phone = user.PhoneNumber;
            city = user.City;
            country = user.Country;
            tenantId = user.TenantId.ToString();
            tenantCode = user.Tenant.Code;
            tenantSlug = user.Tenant.Slug ?? user.Tenant.Code;
            tenantName = user.Tenant.Name;
        }
        else if (credential.OwnerType == CredentialOwnerType.Customer && credential.Customer != null) {
            var customer = credential.Customer;
            displayName = customer.DisplayName ?? credential.Login;
            ownerId = customer.Id.ToString();
            phone = customer.PhoneNumber;
            tenantId = customer.TenantId.ToString();
            tenantCode = customer.Tenant.Code;
            tenantSlug = customer.Tenant.Slug ?? customer.Tenant.Code;
            tenantName = customer.Tenant.Name;
        }

        return (displayName, phone, city, country, tenantId, tenantCode, tenantSlug, tenantName, ownerId);
    }
}
