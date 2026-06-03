using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AuthApi.Models;
using AuthApi.Contracts.Requests;
using AuthApi.Services;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Continuo.Observability.Attributes;

namespace AuthApi.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase {
    private readonly AuthDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly TwoFactorService _twoFactorService;
    private readonly ITrustedDeviceService _trustedDeviceService;
    private readonly PasswordResetService _passwordResetService;
    private readonly IScreenAccessService _screenAccess;
    private readonly ISessionService _sessionService;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        AuthDbContext db,
        ITokenService tokenService,
        TwoFactorService twoFactorService,
        ITrustedDeviceService trustedDeviceService,
        PasswordResetService passwordResetService,
        IScreenAccessService screenAccess,
        ISessionService sessionService,
        IConfiguration config,
        ILogger<AuthController> logger) {
        _db = db;
        _tokenService = tokenService;
        _twoFactorService = twoFactorService;
        _trustedDeviceService = trustedDeviceService;
        _passwordResetService = passwordResetService;
        _screenAccess = screenAccess;
        _sessionService = sessionService;
        _config = config;
        _logger = logger;
    }

    private string? ResolveTrustedDeviceToken() =>
        HttpContext.Request.Headers["X-Trusted-Device-Token"].FirstOrDefault();

    private string ResolveOwnerLogin() =>
        (_config["AUTH:OWNER_LOGIN"]
         ?? _config["AUTH__OWNER_LOGIN"]
         ?? Environment.GetEnvironmentVariable("AUTH_OWNER_LOGIN")
         ?? "platform.owner@example.local").Trim();

    private bool IsPlatformOwner(Credential credential) {
        var ownerLogin = ResolveOwnerLogin();
        return string.Equals(credential.Login?.Trim(), ownerLogin, StringComparison.OrdinalIgnoreCase)
            || string.Equals(credential.Email?.Trim(), ownerLogin, StringComparison.OrdinalIgnoreCase);
    }

    private string? ResolveClientIp() {
        var fwd = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(fwd)) {
            return fwd.Split(',')[0].Trim();
        }
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private string? ResolveUserAgent() => HttpContext.Request.Headers["User-Agent"].FirstOrDefault();

    private string ResolveAppId() {
        var raw = HttpContext.Request.Query["app"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw)) {
            return "default";
        }
        return raw.Trim().ToLowerInvariant();
    }

    [ContinuoProxyMethod("ui")]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req) {
        if (string.IsNullOrWhiteSpace(req.Password)) {
            return BadRequest(new { message = "Password required" });
        }

        var identifier = req.Login?.Trim() ?? req.Email?.Trim();
        if (string.IsNullOrWhiteSpace(identifier)) {
            return BadRequest(new { message = "Login or email required" });
        }

        var credential = await FindCredentialForLoginAsync(identifier, HttpContext.RequestAborted);

        if (credential == null || !credential.IsActive) {
            await Task.Delay(Random.Shared.Next(50, 200));
            return Unauthorized(new { message = "Invalid credentials" });
        }

        var valid = BCrypt.Net.BCrypt.Verify(req.Password, credential.PasswordHash);
        if (!valid) {
            await Task.Delay(Random.Shared.Next(50, 200));
            return Unauthorized(new { message = "Invalid credentials" });
        }

        // PlatformUser ↔ Customer/TenantUser cross-context guard.
        // NOT: Browser cookie domain isolation zaten platform/customer karışmasını
        // önlüyor (env-master ve tenant URL'leri farklı host → farklı cookie scope).
        // Bu guard ek savunma katmanıdır — başarı kriteri:
        //   * env-master URL'inde Customer/TenantUser girişi engellenir.
        //   * Tenant URL'inde TenantUser/Customer kendi tenant'ı dışına oturum açamaz.
        // PlatformUser her iki bağlamda da kabul edilir (super-user / impersonation).
        // Saldırgan password enumeration'a karşı: deny mesajı uniform "Invalid credentials" —
        // gerçek sebep sadece sunucu loguna yazılır, attacker'a creds-validity sızdırılmaz.
        if (!IsContextAllowed(credential, out var contextDenyReason)) {
            _logger.LogInformation("Login context-deny for credential {CredentialId} ({OwnerType}): {Reason}",
                credential.Id, credential.OwnerType, contextDenyReason);
            await Task.Delay(Random.Shared.Next(50, 200));
            return Unauthorized(new { message = "Invalid credentials" });
        }

        if (_twoFactorService.RequiresTwoFactor(credential)) {
            // Trusted-device fast-path: if the browser sent a token issued from a
            // prior 2FA on this credential and it has not expired/been revoked,
            // skip the email challenge and continue straight to JWT issuance.
            var trustedToken = ResolveTrustedDeviceToken();
            var isTrusted = await _trustedDeviceService.IsTrustedAsync(credential.Id, trustedToken, HttpContext.RequestAborted);
            if (!isTrusted) {
                var challenge = await _twoFactorService.CreateChallengeAsync(credential, HttpContext.RequestAborted);
                return Ok(new {
                    requiresTwoFactor = true,
                    challengeId = challenge.Id.ToString(),
                    expiresAtUtc = challenge.ExpiresAtUtc,
                    channel = challenge.Channel,
                    targetHint = MaskTarget(challenge.Target)
                });
            }
        }

        credential.LastLoginAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var authResponse = await BuildAuthResponseAsync(credential);
        return Ok(authResponse);
    }

    [ContinuoProxyMethod("ui")]
    [HttpPost("login/google")]
    public async Task<IActionResult> LoginGoogle([FromBody] LoginGoogleRequest req) {
        if (string.IsNullOrWhiteSpace(req.IdToken)) {
            return BadRequest(new { message = "IdTokenRequired" });
        }

        var clientId =
            _config["GOOGLE:CLIENTID"] ??
            _config["GOOGLE__CLIENTID"] ??
            _config["NEXT_PUBLIC_GOOGLE_CLIENT_ID"];
        if (string.IsNullOrWhiteSpace(clientId)) {
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "GoogleClientIdNotConfigured" });
        }

        GoogleJsonWebSignature.Payload payload;
        try {
            payload = await GoogleJsonWebSignature.ValidateAsync(req.IdToken, new GoogleJsonWebSignature.ValidationSettings {
                Audience = new[] { clientId }
            });
        }
        catch {
            await Task.Delay(Random.Shared.Next(50, 200));
            return Unauthorized(new { message = "InvalidGoogleToken" });
        }

        var email = payload.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email)) {
            return Unauthorized(new { message = "GoogleTokenMissingEmail" });
        }

        var credential = await FindCredentialForLoginAsync(email, HttpContext.RequestAborted);
        if (credential == null || !credential.IsActive) {
            if (req.AutoRegister) {
                return StatusCode(StatusCodes.Status501NotImplemented, new { message = "AutoRegisterNotSupported" });
            }

            return NotFound(new { message = "UserNotFound" });
        }

        // Cross-context guard — bkz. /auth/login üstündeki açıklama
        if (!IsContextAllowed(credential, out var googleContextDenyReason)) {
            _logger.LogInformation("Google login context-deny for credential {CredentialId} ({OwnerType}): {Reason}",
                credential.Id, credential.OwnerType, googleContextDenyReason);
            await Task.Delay(Random.Shared.Next(50, 200));
            return Unauthorized(new { message = "Invalid credentials" });
        }

        if (_twoFactorService.RequiresTwoFactor(credential)) {
            var trustedToken = ResolveTrustedDeviceToken();
            var isTrusted = await _trustedDeviceService.IsTrustedAsync(credential.Id, trustedToken, HttpContext.RequestAborted);
            if (!isTrusted) {
                var challenge = await _twoFactorService.CreateChallengeAsync(credential, HttpContext.RequestAborted);
                return Ok(new {
                    requiresTwoFactor = true,
                    challengeId = challenge.Id.ToString(),
                    expiresAtUtc = challenge.ExpiresAtUtc,
                    channel = challenge.Channel,
                    targetHint = MaskTarget(challenge.Target)
                });
            }
        }

        credential.LastLoginAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var authResponse = await BuildAuthResponseAsync(credential);
        return Ok(authResponse);
    }

    [ContinuoProxyMethod("ui")]
    [HttpPost("password/forgot")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req, CancellationToken ct) {
        var identifier = !string.IsNullOrWhiteSpace(req.Login) ? req.Login.Trim() : req.Email?.Trim();
        if (!string.IsNullOrWhiteSpace(identifier)) {
            await _passwordResetService.RequestResetAsync(identifier, BuildPasswordResetContext(req.ResetPath, req.ResetOrigin), ct);
        }
        else {
            await Task.Delay(Random.Shared.Next(80, 220), ct);
        }

        return Ok(new { ok = true });
    }

    [ContinuoProxyMethod("ui")]
    [HttpPost("password/reset")]
    public async Task<IActionResult> ResetPassword([FromBody] CompletePasswordResetRequest req, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(req.Token) || string.IsNullOrWhiteSpace(req.NewPassword)) {
            return BadRequest(new { message = "TokenAndPasswordRequired" });
        }

        var result = await _passwordResetService.ResetPasswordAsync(req.Token, req.NewPassword, BuildPasswordResetContext(null), ct);
        if (!result.Success) {
            return BadRequest(new { message = result.Error ?? "PasswordResetFailed" });
        }

        return Ok(new { ok = true });
    }

    [ContinuoProxyMethod("ui")]
    [HttpPost("me/change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req, CancellationToken ct) {
        if (User?.Identity?.IsAuthenticated != true) {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(req.CurrentPassword) || string.IsNullOrWhiteSpace(req.NewPassword)) {
            return BadRequest(new { message = "PasswordRequired" });
        }

        if (!PasswordPolicy.Validate(req.NewPassword, out var error)) {
            return BadRequest(new { message = error });
        }

        var credentialId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                           ?? User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                           ?? User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(credentialId) || !Ulid.TryParse(credentialId, out var parsed)) {
            return Unauthorized();
        }

        var credential = await _db.Credentials.FirstOrDefaultAsync(c => c.Id == parsed, ct);
        if (credential == null || !credential.IsActive) {
            return Unauthorized();
        }

        var valid = BCrypt.Net.BCrypt.Verify(req.CurrentPassword, credential.PasswordHash);
        if (!valid) {
            await Task.Delay(Random.Shared.Next(50, 200), ct);
            return Unauthorized(new { message = "InvalidCredentials" });
        }

        if (BCrypt.Net.BCrypt.Verify(req.NewPassword, credential.PasswordHash)) {
            return BadRequest(new { message = "PasswordMustBeDifferent" });
        }

        credential.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        credential.MustChangePassword = false;
        credential.PasswordChangedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new { ok = true });
    }

    [ContinuoProxyMethod("ui")]
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshToken() {
        // Opaque-session refresh path: BFF holds only the opaque token, sends
        // it via X-Session-Token. We look up the live session, hydrate the
        // credential, and mint a fresh JWT keyed to the same sid. No JWT
        // re-validation needed because the session row is the source of truth.
        var opaqueSessionToken = ResolveOpaqueSessionToken();
        if (!string.IsNullOrWhiteSpace(opaqueSessionToken)) {
            var session = await _sessionService.ResolveByOpaqueTokenAsync(opaqueSessionToken, HttpContext.RequestAborted);
            if (session == null) {
                return Unauthorized(new { message = "session_replaced", reason = "session_replaced" });
            }

            var credentialFromSession = await LoadCredentialAsync(session.CredentialId, HttpContext.RequestAborted);
            if (credentialFromSession == null || !credentialFromSession.IsActive) {
                return Unauthorized(new { message = "Invalid credentials" });
            }

            if (!IsContextAllowed(credentialFromSession, out var sessionContextDenyReason)) {
                return Unauthorized(new { message = sessionContextDenyReason });
            }

            var sessionResponse = await BuildAuthResponseAsync(credentialFromSession, session.Id);
            return Ok(sessionResponse);
        }

        // Legacy JWT-bearer refresh path (kept during the rolling deploy window —
        // older browsers still hold a JWT cookie until they re-login).
        var token = ResolveToken();
        if (string.IsNullOrWhiteSpace(token)) {
            return Unauthorized(new { message = "Missing token" });
        }

        var principal = ValidateToken(token);
        if (principal == null) {
            return Unauthorized(new { message = "Invalid token" });
        }

        var credentialIdClaim = principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub || c.Type == "sub")?.Value
                               ?? principal.Claims.FirstOrDefault(c => c.Type == System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(credentialIdClaim) || !Ulid.TryParse(credentialIdClaim, out var credentialId)) {
            return Unauthorized(new { message = "Invalid token" });
        }

        var credential = await LoadCredentialAsync(credentialId, HttpContext.RequestAborted);
        if (credential == null || !credential.IsActive) {
            return Unauthorized(new { message = "Invalid credentials" });
        }

        // Cross-context guard refresh sırasında da geçerli — login'de uygulanan
        // PlatformUser / Customer / Tenant kuralları burada da çalışmalı; aksi
        // halde tenant-A'da login olan kullanıcı tenant-B URL'inden refresh
        // çağrısı yapıp B context'inde yeni JWT alabilir.
        if (!IsContextAllowed(credential, out var refreshContextDenyReason)) {
            return Unauthorized(new { message = refreshContextDenyReason });
        }

        // Session enforcement: tek-aktif-oturum kuralı için sid claim'ini doğrula.
        // Token'da sid varsa (yeni token), DB'deki UserSession revoked olmamalı —
        // aksi halde başka bir cihazdan login olunmuş demektir, "session_replaced"
        // ile 401 dön. Token'da sid yoksa (eski/legacy token, deploy geçişi) yeni
        // bir session oluştur — geçiş penceresinde graceful davranış.
        Ulid? sessionId = null;
        var sidClaim = principal.Claims.FirstOrDefault(c => c.Type == "sid")?.Value;
        if (!string.IsNullOrWhiteSpace(sidClaim) && Ulid.TryParse(sidClaim, out var parsedSid)) {
            var session = await _sessionService.TouchActiveAsync(parsedSid, HttpContext.RequestAborted);
            if (session == null) {
                return Unauthorized(new { message = "session_replaced", reason = "session_replaced" });
            }
            sessionId = session.Id;
        }

        var response = await BuildAuthResponseAsync(credential, sessionId);
        return Ok(response);
    }

    private Task<Credential?> LoadCredentialAsync(Ulid credentialId, CancellationToken ct) {
        return _db.Credentials
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
            .Include(c => c.Customer)
            .FirstOrDefaultAsync(c => c.Id == credentialId, ct);
    }

    private string? ResolveOpaqueSessionToken() {
        var raw = HttpContext.Request.Headers["X-Session-Token"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(raw)) {
            return raw.Trim();
        }

        // Fallback: orchestrator may convert an opaque cookie into Authorization: Bearer
        // before forwarding to auth-api. A non-3-segment value is an opaque session
        // token; only treat 3-dot inputs as a real JWT.
        var bearer = ResolveToken();
        if (!string.IsNullOrWhiteSpace(bearer) && !LooksLikeJwt(bearer)) {
            return bearer.Trim();
        }

        return null;
    }

    private static bool LooksLikeJwt(string value) {
        var parts = value.Split('.');
        return parts.Length == 3 && !string.IsNullOrEmpty(parts[0]) && !string.IsNullOrEmpty(parts[1]) && !string.IsNullOrEmpty(parts[2]);
    }

    [ContinuoProxyMethod("ui")]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout() {
        // Idempotent: çağrı her zaman 200 döner. sid yok / token süresi geçmiş
        // olabilir; bu durumlarda da front-end'in cookie temizliği bozulmasın.
        var opaqueSessionToken = ResolveOpaqueSessionToken();
        if (!string.IsNullOrWhiteSpace(opaqueSessionToken)) {
            await _sessionService.RevokeByOpaqueTokenAsync(opaqueSessionToken, UserSessionRevocationReasons.Logout, HttpContext.RequestAborted);
            return Ok(new { ok = true });
        }

        var token = ResolveToken();
        if (!string.IsNullOrWhiteSpace(token)) {
            try {
                var handler = new JwtSecurityTokenHandler();
                if (handler.CanReadToken(token)) {
                    var jwt = handler.ReadJwtToken(token);
                    var sidClaim = jwt.Claims.FirstOrDefault(c => c.Type == "sid")?.Value;
                    if (!string.IsNullOrWhiteSpace(sidClaim) && Ulid.TryParse(sidClaim, out var sid)) {
                        await _sessionService.RevokeAsync(sid, UserSessionRevocationReasons.Logout, HttpContext.RequestAborted);
                    }
                }
            } catch (Exception ex) {
                _logger.LogDebug(ex, "Logout: failed to parse token for session revoke; ignoring");
            }
        }

        return Ok(new { ok = true });
    }

    [ContinuoProxyMethod("ui")]
    [HttpPost("login/verify-otp")]
    public async Task<IActionResult> VerifyTwoFactor([FromBody] VerifyTwoFactorRequest req) {
        if (string.IsNullOrWhiteSpace(req.ChallengeId) || string.IsNullOrWhiteSpace(req.Code)) {
            return BadRequest(new { message = "ChallengeIdAndCodeRequired" });
        }

        var result = await _twoFactorService.VerifyAsync(req.ChallengeId, req.Code, HttpContext.RequestAborted);
        if (!result.Success || result.Credential == null) {
            return Unauthorized(new { message = result.Error ?? "VerificationFailed" });
        }

        // After a successful 2FA, issue a per-device trust token so this browser
        // is allowed to bypass 2FA on subsequent logins until the TTL expires
        // (TwoFactorOptions.TrustedDeviceTtlDays, default 30 days). The raw
        // token is returned to the BFF to set as an HttpOnly cookie — only the
        // SHA-256 hash is persisted server-side.
        var trustedDeviceToken = await _trustedDeviceService.IssueAsync(
            result.Credential.Id,
            ResolveUserAgent(),
            ResolveClientIp(),
            HttpContext.RequestAborted);

        var response = await BuildAuthResponseAsync(result.Credential, trustedDeviceToken: trustedDeviceToken);
        return Ok(response);
    }

    [ContinuoProxyMethod("ui")]
    [HttpPost("login/resend-otp")]
    public async Task<IActionResult> ResendTwoFactor([FromBody] ResendTwoFactorRequest req) {
        if (string.IsNullOrWhiteSpace(req.ChallengeId)) {
            return BadRequest(new { message = "ChallengeIdRequired" });
        }

        var result = await _twoFactorService.ResendChallengeAsync(req.ChallengeId, HttpContext.RequestAborted);
        if (!result.Success) {
            if (result.RetryAfterUtc.HasValue) {
                return StatusCode(
                    StatusCodes.Status429TooManyRequests,
                    new { message = result.Error, retryAfterUtc = result.RetryAfterUtc.Value });
            }
            return BadRequest(new { message = result.Error ?? "ResendFailed" });
        }

        return Ok(new { ok = true, expiresAtUtc = result.ExpiresAtUtc });
    }


    private async Task<object> BuildAuthResponseAsync(Credential credential, Ulid? existingSessionId = null, string? trustedDeviceToken = null) {
        var httpContext = HttpContext ?? throw new InvalidOperationException("HttpContext is not available.");
        if (httpContext.Request == null) {
            throw new InvalidOperationException("HttpContext.Request is not available.");
        }

        var ownerRoles = ResolveRoles(credential).ToList();
        var userRoles = ResolveUserRoles(credential).ToList();
        var roleNames = ownerRoles.Select(r => r.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var rolePermissions = ownerRoles.SelectMany(r => r.Permissions.Select(p => p.PermissionKey));
        // Customer credentials get the customer.* maestro permissions automatically
        // — we don't seed a CustomerStandard role because qrmenu customers come
        // through a different signup pipeline (no admin role assignment). This
        // keeps the wallet + chat surfaces accessible to every authenticated guest
        // without explicit role admin work. Plan: MAESTRO_TOKEN_WALLET_PLAN.md §7.
        var permissionKeys = (credential.OwnerType == CredentialOwnerType.Customer
                ? rolePermissions.Concat(new[] {
                    AuthApi.Permissions.PermissionKeys.Customer.MaestroUse,
                    AuthApi.Permissions.PermissionKeys.Customer.MaestroBillingManage
                  })
                : rolePermissions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var branchCodes = userRoles
            .Where(ur => !string.IsNullOrWhiteSpace(ur.BranchCode))
            .Select(ur => ur.BranchCode!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Format: "branchCode:roleName" for granular branch-role mapping
        var branchRoleClaims = userRoles
            .Where(ur => !string.IsNullOrWhiteSpace(ur.BranchCode))
            .Select(ur => $"{ur.BranchCode}:{ur.Role.Name}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var appCode = httpContext.Request.Query["app"].FirstOrDefault();
        var screens = await _screenAccess.ResolveScreensAsync(credential, permissionKeys, ownerRoles, appCode, httpContext.RequestAborted);

        // Single-active-session enforcement: on a fresh login, create a UserSession
        // row + opaque session token. For platform.owner the displacement step is skipped
        // (multi-device access is permitted). On refresh, we re-use the existing session id.
        // Opaque sessionToken is only returned to the BFF on the original /auth/login call;
        // refresh/2FA-verify paths re-use the existing session row and don't surface it again
        // because the BFF already has the cookie set.
        var sessionId = existingSessionId;
        string? opaqueSessionToken = null;
        if (sessionId == null) {
            var (session, opaque) = await _sessionService.CreateAsync(
                credential.Id,
                ResolveAppId(),
                exemptFromSingleSession: IsPlatformOwner(credential),
                ResolveClientIp(),
                ResolveUserAgent(),
                httpContext.RequestAborted);
            sessionId = session.Id;
            opaqueSessionToken = opaque;
        }

        var (accessToken, accessExpires) = await _tokenService.CreateAccessTokenAsync(credential, roleNames, permissionKeys, screens, branchCodes, branchRoleClaims, sessionId);
        var (refreshToken, refreshExpires) = await _tokenService.CreateRefreshTokenAsync();

        var branchRolesResponse = userRoles
            .Select(ur => new { roleId = ur.RoleId.ToString(), roleName = ur.Role.Name, branchCode = ur.BranchCode })
            .ToArray();

        return new {
            accessToken,
            accessExpires,
            // Opaque session token — BFF must store this in the HttpOnly browser
            // cookie instead of the bloated JWT. The BFF exchanges it back to a
            // fresh JWT on every downstream call (per-process cached). See
            // SessionExchangeController + ui/packages/bff session helpers.
            sessionToken = opaqueSessionToken,
            sessionExpires = (DateTime?)null,
            refreshToken,
            refreshExpires,
            trustedDeviceToken,
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
                branchCodes,
                branchRoles = branchRolesResponse,
                tenant = ResolveTenantSummary(credential),
                agreementsAccepted = credential.AgreementsAcceptedAtUtc.HasValue,
                agreementsAcceptedAtUtc = credential.AgreementsAcceptedAtUtc,
                mustChangePassword = credential.MustChangePassword
            }
        };
    }

    private Task<Credential?> FindCredentialForLoginAsync(string identifier, CancellationToken ct) {
        var normalized = identifier.Trim();
        // Sadece aktif credential'lar arasinda ara. Pasif credential ayni Login ile
        // veritabaninda kalabilir (audit) ama login akisina dahil edilmemeli.
        return _db.Credentials
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
            .Include(c => c.Customer)
            .FirstOrDefaultAsync(c => c.IsActive && (c.Login == normalized || c.Email == normalized), ct);
    }

    /// <summary>
    /// App-aware platform UI listesi — bu UI'lara sadece PlatformUser girer
    /// (ortam farketmez). Tenant admin app'leri (console-admin) ve customer-
    /// facing app'ler ayri kurallarla degerlendirilir.
    /// </summary>
    private static readonly HashSet<string> PlatformOnlyApps = new(StringComparer.OrdinalIgnoreCase) {
        "tc-ops-ui",
        "dev-support-console",
        "tcc-ops-ui",
        "tcc-ui"
    };

    private static readonly HashSet<string> CustomerFacingApps = new(StringComparer.OrdinalIgnoreCase) {
        "qrmenu-web",
        "qrmenu-mobile",
        "public-web",
        "continuo-web"
    };

    /// <summary>
    /// Login isteğinde URL ve hedef UI app context'ine göre kim girebilir kararı.
    ///
    /// Kurallar (X-Client-App + X-Tenant-Slug + X-Env-Prefix tabanlı):
    /// - Platform-only app (tc-ops-ui, dev-support-console, vs.) → her ortamda
    ///   yalnızca PlatformUser. Tenant/Customer reddedilir.
    /// - Tenant admin app (console-admin):
    ///   * X-Tenant-Slug yok (env-master URL gibi) → yalnız PlatformUser.
    ///   * X-Tenant-Slug var → PlatformUser her zaman OK, TenantUser ise kendi
    ///     tenant.Slug'i eşleşiyorsa OK (eski versiyonda Code ile karşılaştırılıyordu;
    ///     seeded `t-001 / continuo` tipi case'lerde slug != code → guard yanlış
    ///     bloklamıştı. Slug-first, code-fallback ile düzeltildi).
    /// - Customer-facing app (qrmenu-*, public-web, vs.):
    ///   * Customer can authenticate only when the target app is explicitly
    ///     listed as customer-facing and a tenant context exists.
    ///   * Customer must not authenticate into console-admin or staff apps;
    ///     those apps rely on PlatformUser/TenantUser flows.
    ///
    /// Trust boundary: <c>X-Env-Prefix</c> header'ına yalnızca beraberinde
    /// geçerli <c>X-M2M-API-KEY</c> geldiğinde güvenilir. Orchestrator browser
    /// proxy'sinde her ikisini birden set eder; başka bir caller (doğrudan
    /// auth-api'ye giden istek) header'ı spoof'lasa bile M2M key olmadan
    /// dikkate alınmaz — Customer "production-cafezero" hostundan giriş
    /// yapıyormuş gibi guard'ı bypass edemez.
    /// </summary>
    private bool IsContextAllowed(Credential credential, out string reason) {
        var tenantSlug = HttpContext.Request.Headers["X-Tenant-Slug"].FirstOrDefault()?.Trim().ToLowerInvariant();
        var envPrefix = HttpContext.Request.Headers["X-Env-Prefix"].FirstOrDefault()?.Trim().ToLowerInvariant();
        // Client app: ?app=...  query veya X-Client-App header (BFF her ikisini set ediyor).
        var clientApp = HttpContext.Request.Query["app"].FirstOrDefault()?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(clientApp)) {
            clientApp = HttpContext.Request.Headers["X-Client-App"].FirstOrDefault()?.Trim().ToLowerInvariant();
        }

        // X-Env-Prefix trust kontrolü: yalnız orchestrator'dan (M2M key ile birlikte)
        // gelmişse honour edilir, aksi halde sessiz ignore (header yokmuş gibi).
        if (!string.IsNullOrEmpty(envPrefix) && !IsTrustedInternalEnvHeader()) {
            _logger.LogWarning(
                "X-Env-Prefix header received without valid M2M key from {RemoteIp}; ignoring (potential spoof)",
                HttpContext.Connection.RemoteIpAddress);
            envPrefix = null;
        }

        var isPlatformOnlyApp = !string.IsNullOrEmpty(clientApp) && PlatformOnlyApps.Contains(clientApp);
        var isCustomerFacingApp = !string.IsNullOrEmpty(clientApp) && CustomerFacingApps.Contains(clientApp);

        // 1) Platform-only app (tc-ops-ui, dev-support-console, …) → PlatformUser only,
        //    ortam fark etmez. Tenant/Customer reddedilir.
        if (isPlatformOnlyApp) {
            if (credential.OwnerType == CredentialOwnerType.PlatformUser) {
                reason = string.Empty;
                return true;
            }
            reason = $"PlatformUserOnlyOnApp:{clientApp}";
            return false;
        }

        // Helper: credential'in tenant slug'i (slug-first, code-fallback) — eski
        // guard sadece code karsilastiriyor, slug != code durumunda (seeded
        // t-001/continuo gibi) gercek tenant kullanicisini reddediyordu.
        string? GetCredentialTenantKey() {
            var t = credential.OwnerType switch {
                CredentialOwnerType.TenantUser => credential.TenantUser?.Tenant,
                CredentialOwnerType.Customer => credential.Customer?.Tenant,
                _ => null
            };
            if (t == null) {
                return null;
            }
            var slug = t.Slug?.Trim().ToLowerInvariant();
            if (!string.IsNullOrEmpty(slug)) {
                return slug;
            }
            var code = t.Code?.Trim().ToLowerInvariant();
            return string.IsNullOrEmpty(code) ? null : code;
        }

        // 2) Env-prefix URL'leri (dev-/staging-/test-) — staff/tenant-admin/customer:
        //    * env-tenant (dev-continuo.…): tenantSlug var → console-admin gibi
        //      tenant admin app'lerinde TenantUser kendi slug'iyle girer; qrmenu-*
        //      app'lerinde Customer ana-tenant != islem-tenant olabilir (musteri
        //      A tenant'inda kayitli, B tenant'inin subesinden siparis verir —
        //      loyalty + cross-branch akisi normaldir).
        //    * env-master (dev-console-admin.…): tenantSlug yok → PlatformUser only.
        //    * TenantUser: slug eslesmesi zorunlu (staff cross-tenant kacagi yok).
        //    * Customer: tenantSlug context'i mevcut olsun yeter; ana tenant'i
        //      farkli olabilir.
        //    Prod-prefix (faz 9 sonrasi) icin ayri kisitlar burada eklenecek —
        //    su an GKE shutdown sonrasi sadece dev/staging yasiyor.
        if (!string.IsNullOrEmpty(envPrefix)) {
            if (credential.OwnerType == CredentialOwnerType.PlatformUser) {
                reason = string.Empty;
                return true;
            }
            if (credential.OwnerType == CredentialOwnerType.Customer) {
                if (!isCustomerFacingApp) {
                    reason = $"CustomerNotAllowedOnApp:{clientApp ?? "(missing)"}";
                    return false;
                }
                if (string.IsNullOrEmpty(tenantSlug)) {
                    reason = "CustomerRequiresTenantContext";
                    return false;
                }
                // Customer ana tenant'i != URL tenant'i OK — cross-branch loyalty/
                // siparis akisi destekleniyor. Sadece URL'in bir tenant context'i
                // tasidigi (env-master olmadigi) dogrulanir.
                reason = string.Empty;
                return true;
            }
            if (credential.OwnerType == CredentialOwnerType.TenantUser) {
                if (string.IsNullOrEmpty(tenantSlug)) {
                    reason = "TenantUserRequiresTenantContext";
                    return false;
                }
                var credKey = GetCredentialTenantKey();
                if (string.IsNullOrEmpty(credKey)) {
                    reason = "CredentialMissingTenantBinding";
                    return false;
                }
                if (!string.Equals(credKey, tenantSlug, StringComparison.Ordinal)) {
                    reason = $"TenantMismatch:{credKey}!={tenantSlug}";
                    return false;
                }
                reason = string.Empty;
                return true;
            }
            reason = "UnknownOwnerType";
            return false;
        }

        // 3) No env-prefix (production tenant URL veya apex):
        if (string.IsNullOrEmpty(tenantSlug)) {
            // apex/internal call (cafezero olmayan, mert.cengiz değil).
            if (credential.OwnerType == CredentialOwnerType.PlatformUser) {
                reason = string.Empty;
                return true;
            }
            reason = "PlatformUserOnlyWithoutTenantContext";
            return false;
        }

        if (credential.OwnerType == CredentialOwnerType.PlatformUser) {
            // PlatformUser cross-tenant super-user
            reason = string.Empty;
            return true;
        }

        // Customer prod tenant URL'inde: ana tenant != URL tenant olabilir
        // (cross-branch loyalty / siparis akisi). Sadece tenant context'i
        // mevcut olsun yeter — env-prefix path'iyle simetrik davranis.
        if (credential.OwnerType == CredentialOwnerType.Customer) {
            if (!isCustomerFacingApp) {
                reason = $"CustomerNotAllowedOnApp:{clientApp ?? "(missing)"}";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        // TenantUser prod tenant URL'inde slug match şart (staff cross-tenant kacagi yok)
        var tenantKey = GetCredentialTenantKey();
        if (string.IsNullOrEmpty(tenantKey)) {
            reason = "CredentialMissingTenantBinding";
            return false;
        }
        if (!string.Equals(tenantKey, tenantSlug, StringComparison.Ordinal)) {
            reason = $"TenantMismatch:{tenantKey}!={tenantSlug}";
            return false;
        }
        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// X-Env-Prefix gibi orchestrator-only sinyallere güvenilebilmesi için
    /// caller'ın <c>X-M2M-API-KEY</c> header'ında platform M2M key'ini
    /// göndermesi gerekir. Sabit-zaman karşılaştırma timing-attack'a karşı
    /// (FixedTimeEquals).
    /// </summary>
    private bool IsTrustedInternalEnvHeader() {
        var providedKey = HttpContext.Request.Headers["X-M2M-API-KEY"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(providedKey)) {
            return false;
        }

        var expectedKey = _config["M2M:ApiKey"]
                          ?? _config["M2M__API_KEY"]
                          ?? _config["M2M_API_KEY"];
        if (string.IsNullOrWhiteSpace(expectedKey)) {
            return false;
        }

        var providedBytes = Encoding.UTF8.GetBytes(providedKey);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedKey);
        return providedBytes.Length == expectedBytes.Length
               && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
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

        if (credential.OwnerType == CredentialOwnerType.Customer && credential.Customer?.Tenant != null) {
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

    private static IEnumerable<UserRole> ResolveUserRoles(Credential credential) {
        return credential.OwnerType switch {
            CredentialOwnerType.PlatformUser => credential.PlatformUser?.Roles ?? Enumerable.Empty<UserRole>(),
            CredentialOwnerType.TenantUser => credential.TenantUser?.Roles ?? Enumerable.Empty<UserRole>(),
            _ => Enumerable.Empty<UserRole>()
        };
    }

    private ClaimsPrincipal? ValidateToken(string token) {
        var secret = _config["JWT:SECRET"] ?? _config["JWT__SECRET"];
        if (string.IsNullOrWhiteSpace(secret)) {
            return null;
        }

        var issuer = _config["JWT:ISSUER"] ?? _config["JWT__ISSUER"];
        var audience = _config["JWT:AUDIENCE"] ?? _config["JWT__AUDIENCE"];

        var validationParams = new TokenValidationParameters {
            ValidateIssuer = !string.IsNullOrWhiteSpace(issuer),
            ValidIssuer = issuer,
            ValidateAudience = !string.IsNullOrWhiteSpace(audience),
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        try {
            var handler = new JwtSecurityTokenHandler();
            return handler.ValidateToken(token, validationParams, out _);
        } catch {
            return null;
        }
    }

    private string? ResolveToken() {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) {
            return authHeader.Substring("Bearer ".Length).Trim();
        }

        var cookie = Request.Cookies["auth_token"];
        if (!string.IsNullOrWhiteSpace(cookie)) {
            return cookie;
        }

        if (Request.Query.TryGetValue("token", out var tokenValues)) {
            return tokenValues.FirstOrDefault();
        }

        return null;
    }

    private PasswordResetContext BuildPasswordResetContext(string? resetPath, string? resetOrigin = null) {
        var appId = HttpContext.Request.Query["app"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(appId)) {
            appId = HttpContext.Request.Headers["X-Client-App"].FirstOrDefault();
        }

        var origin = resetOrigin;
        if (string.IsNullOrWhiteSpace(origin)) {
            origin = HttpContext.Request.Headers["Origin"].FirstOrDefault();
        }
        if (string.IsNullOrWhiteSpace(origin)) {
            origin = HttpContext.Request.Headers["Referer"].FirstOrDefault();
        }

        return new PasswordResetContext(
            string.IsNullOrWhiteSpace(appId) ? "default" : appId.Trim().ToLowerInvariant(),
            HttpContext.Request.Headers["X-Tenant-Slug"].FirstOrDefault(),
            origin,
            resetPath,
            ResolveClientIp(),
            ResolveUserAgent());
    }
}
