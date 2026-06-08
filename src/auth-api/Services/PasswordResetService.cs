using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuthApi.Models;
using Microsoft.EntityFrameworkCore;
using Continuo.Persistence.Outbox;
using Continuo.Shared.Contracts;

namespace AuthApi.Services;

public sealed record PasswordResetContext(
    string AppId,
    string? TenantSlug,
    string? Origin,
    string? ResetPath,
    string? IpAddress,
    string? UserAgent);

public sealed class PasswordResetService {
    private const int TokenTtlMinutes = 30;
    private const int MaxAttempts = 5;
    private static readonly HashSet<string> PlatformOnlyApps = new(StringComparer.OrdinalIgnoreCase) {
        "continuo-ops-ui",
        "dev-support-console",
    };
    private static readonly HashSet<string> CustomerFacingApps = new(StringComparer.OrdinalIgnoreCase) {
        "qrmenu-web",
        "qrmenu-mobile",
        "public-web",
        "continuo-web"
    };

    private readonly AuthDbContext _db;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(AuthDbContext db, ILogger<PasswordResetService> logger) {
        _db = db;
        _logger = logger;
    }

    public async Task RequestResetAsync(string identifier, PasswordResetContext context, CancellationToken ct) {
        var normalizedIdentifier = NormalizeIdentifier(identifier);
        var normalizedAppId = NormalizeAppId(context.AppId);
        if (string.IsNullOrWhiteSpace(normalizedIdentifier)) {
            await UniformDelayAsync(ct);
            return;
        }

        var credential = await FindCredentialForResetAsync(normalizedIdentifier, normalizedAppId, context.TenantSlug, ct);
        if (credential == null) {
            await UniformDelayAsync(ct);
            return;
        }

        var origin = NormalizeOrigin(context.Origin);
        var resetPath = NormalizeResetPath(context.ResetPath);
        if (resetPath != null && !IsResetPathAllowedForApp(normalizedAppId, resetPath)) {
            resetPath = null;
        }
        if (origin == null || resetPath == null) {
            _logger.LogWarning(
                "Password reset skipped for credential {CredentialId}: invalid origin or reset path. app={AppId} originHost={OriginHost} resetPathPresent={ResetPathPresent}",
                credential.Id,
                normalizedAppId,
                TryResolveHost(context.Origin),
                !string.IsNullOrWhiteSpace(context.ResetPath));
            await UniformDelayAsync(ct);
            return;
        }

        var now = DateTime.UtcNow;
        var rawToken = GenerateToken();
        var token = new PasswordResetToken {
            Id = Ulid.NewUlid(),
            CredentialId = credential.Id,
            AppId = normalizedAppId,
            TokenHash = HashToken(rawToken),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(TokenTtlMinutes),
            RequestIp = Truncate(context.IpAddress, 64),
            UserAgent = Truncate(context.UserAgent, 512)
        };

        var activeTokens = await _db.PasswordResetTokens
            .Where(t => t.CredentialId == credential.Id
                && t.AppId == normalizedAppId
                && t.ConsumedAtUtc == null
                && t.ExpiresAtUtc > now)
            .ToListAsync(ct);
        foreach (var active in activeTokens) {
            active.ConsumedAtUtc = now;
        }

        _db.PasswordResetTokens.Add(token);
        _db.OutboxMessages.Add(new OutboxMessage {
            Type = PasswordResetEventTypes.Requested,
            Payload = JsonSerializer.Serialize(new PasswordResetRequestedEvent(
                token.Id.ToString(),
                normalizedAppId,
                ResolveTarget(credential),
                BuildResetUrl(origin, resetPath, rawToken),
                token.ExpiresAtUtc,
                ResolveDisplayName(credential),
                ResolveTenantName(credential))),
            OccurredOn = now,
            Processed = false
        });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Queued password reset notification for credential {CredentialId} on app {AppId}", credential.Id, normalizedAppId);
    }

    public async Task<(bool Success, string? Error)> ResetPasswordAsync(string rawToken, string newPassword, PasswordResetContext context, CancellationToken ct) {
        var normalizedAppId = NormalizeAppId(context.AppId);
        var tokenHash = HashToken(rawToken);
        var now = DateTime.UtcNow;

        var token = await _db.PasswordResetTokens
            .Include(t => t.Credential)
                .ThenInclude(c => c.PlatformUser)
            .Include(t => t.Credential)
                .ThenInclude(c => c.TenantUser)
                    .ThenInclude(u => u!.Tenant)
            .Include(t => t.Credential)
                .ThenInclude(c => c.Customer)
                    .ThenInclude(c => c!.Tenant)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

        if (token == null || !string.Equals(token.AppId, normalizedAppId, StringComparison.OrdinalIgnoreCase)) {
            await UniformDelayAsync(ct);
            return (false, "InvalidOrExpiredToken");
        }

        if (!token.IsActive(now) || token.AttemptCount >= MaxAttempts) {
            token.AttemptCount += 1;
            await _db.SaveChangesAsync(ct);
            await UniformDelayAsync(ct);
            return (false, "InvalidOrExpiredToken");
        }

        token.AttemptCount += 1;

        if (!IsCredentialAllowedForApp(token.Credential, normalizedAppId, context.TenantSlug)) {
            await _db.SaveChangesAsync(ct);
            await UniformDelayAsync(ct);
            return (false, "InvalidOrExpiredToken");
        }

        if (!PasswordPolicy.Validate(newPassword, out var error)) {
            await _db.SaveChangesAsync(ct);
            return (false, error);
        }

        if (BCrypt.Net.BCrypt.Verify(newPassword, token.Credential.PasswordHash)) {
            await _db.SaveChangesAsync(ct);
            return (false, "PasswordMustBeDifferent");
        }

        token.Credential.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        token.Credential.MustChangePassword = false;
        token.Credential.PasswordChangedAtUtc = now;
        token.ConsumedAtUtc = now;

        var sessions = await _db.UserSessions
            .Where(s => s.CredentialId == token.CredentialId && s.RevokedAtUtc == null)
            .ToListAsync(ct);
        foreach (var session in sessions) {
            session.RevokedAtUtc = now;
            session.RevokedReason = UserSessionRevocationReasons.PasswordReset;
        }

        var trustedDevices = await _db.TrustedDevices
            .Where(d => d.CredentialId == token.CredentialId && d.RevokedAtUtc == null)
            .ToListAsync(ct);
        foreach (var device in trustedDevices) {
            device.RevokedAtUtc = now;
        }

        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    private async Task<Credential?> FindCredentialForResetAsync(string identifier, string appId, string? tenantSlug, CancellationToken ct) {
        var candidates = await _db.Credentials
            .IgnoreQueryFilters()
            .Include(c => c.PlatformUser)
            .Include(c => c.TenantUser)
                .ThenInclude(u => u!.Tenant)
            .Include(c => c.Customer)
                .ThenInclude(c => c!.Tenant)
            .Where(c => c.IsActive && (c.Login == identifier || c.Email == identifier))
            .OrderBy(c => c.OwnerType)
            .ToListAsync(ct);

        return candidates.FirstOrDefault(c => IsCredentialAllowedForApp(c, appId, tenantSlug));
    }

    private static bool IsCredentialAllowedForApp(Credential credential, string appId, string? tenantSlug) {
        if (!credential.IsActive) {
            return false;
        }

        if (PlatformOnlyApps.Contains(appId)) {
            return credential.OwnerType == CredentialOwnerType.PlatformUser
                && credential.PlatformUser?.IsActive == true;
        }

        if (CustomerFacingApps.Contains(appId)) {
            return credential.OwnerType == CredentialOwnerType.Customer
                && credential.CustomerId.HasValue
                && credential.Customer != null;
        }

        if (string.Equals(appId, "console-admin", StringComparison.OrdinalIgnoreCase)) {
            if (credential.OwnerType == CredentialOwnerType.PlatformUser) {
                return credential.PlatformUser?.IsActive == true;
            }

            if (credential.OwnerType != CredentialOwnerType.TenantUser || credential.TenantUser?.IsActive != true) {
                return false;
            }

            var requestedTenant = NormalizeTenantKey(tenantSlug);
            if (string.IsNullOrEmpty(requestedTenant)) {
                return false;
            }

            var credentialTenant = ResolveTenantKey(credential.TenantUser.Tenant);
            return string.Equals(credentialTenant, requestedTenant, StringComparison.Ordinal);
        }

        return false;
    }

    private static string ResolveTarget(Credential credential) =>
        credential.Email
        ?? credential.OwnerType switch {
            CredentialOwnerType.PlatformUser => credential.PlatformUser?.Email,
            CredentialOwnerType.TenantUser => credential.TenantUser?.Email,
            CredentialOwnerType.Customer => credential.Customer?.Email,
            _ => null
        }
        ?? credential.Login;

    private static string? ResolveDisplayName(Credential credential) =>
        credential.OwnerType switch {
            CredentialOwnerType.PlatformUser => credential.PlatformUser?.DisplayName,
            CredentialOwnerType.TenantUser => credential.TenantUser?.DisplayName,
            CredentialOwnerType.Customer => credential.Customer?.DisplayName ?? credential.Customer?.FullName,
            _ => null
        };

    private static string? ResolveTenantName(Credential credential) =>
        credential.OwnerType switch {
            CredentialOwnerType.TenantUser => credential.TenantUser?.Tenant?.Name,
            CredentialOwnerType.Customer => credential.Customer?.Tenant?.Name,
            _ => null
        };

    private static string BuildResetUrl(string origin, string resetPath, string token) {
        var separator = resetPath.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{origin}{resetPath}{separator}token={Uri.EscapeDataString(token)}";
    }

    private static string GenerateToken() {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashToken(string rawToken) {
        if (string.IsNullOrWhiteSpace(rawToken)) {
            return string.Empty;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken.Trim()));
        return Convert.ToHexString(bytes);
    }

    private static string NormalizeIdentifier(string? identifier) =>
        string.IsNullOrWhiteSpace(identifier) ? string.Empty : identifier.Trim().ToLowerInvariant();

    private static string NormalizeAppId(string? appId) =>
        string.IsNullOrWhiteSpace(appId) ? "default" : appId.Trim().ToLowerInvariant();

    private static string? NormalizeOrigin(string? origin) {
        if (string.IsNullOrWhiteSpace(origin) || !Uri.TryCreate(origin.Trim(), UriKind.Absolute, out var uri)) {
            return null;
        }
        if (uri.Scheme != Uri.UriSchemeHttps && uri.Host != "localhost" && uri.Host != "127.0.0.1") {
            return null;
        }
        if (!IsTrustedResetHost(uri.Host)) {
            return null;
        }
        return uri.GetLeftPart(UriPartial.Authority);
    }

    private static bool IsTrustedResetHost(string host) {
        if (string.IsNullOrWhiteSpace(host)) {
            return false;
        }

        return host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || host.Equals("example.local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".example.local", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeResetPath(string? resetPath) {
        if (string.IsNullOrWhiteSpace(resetPath)) {
            return null;
        }
        var path = resetPath.Trim();
        if (path.Length == 0 || path[0] != '/' || path.StartsWith("//", StringComparison.Ordinal)) {
            return null;
        }
        return path.Length > 256 ? null : path;
    }

    private static bool IsResetPathAllowedForApp(string appId, string resetPath) {
        if (string.IsNullOrWhiteSpace(resetPath)) {
            return false;
        }

        if (PlatformOnlyApps.Contains(appId)) {
            return appId switch {
                "dev-support-console" => string.Equals(resetPath, "/devops/reset-password", StringComparison.Ordinal),
                "continuo-ops-ui" => string.Equals(resetPath, "/ops/reset-password", StringComparison.Ordinal),
                _ => false
            };
        }

        if (string.Equals(appId, "console-admin", StringComparison.OrdinalIgnoreCase)) {
            return string.Equals(resetPath, "/admin/reset-password", StringComparison.Ordinal);
        }

        if (string.Equals(appId, "qrmenu-mobile", StringComparison.OrdinalIgnoreCase)) {
            return string.Equals(resetPath, "/m/reset-password", StringComparison.Ordinal);
        }

        if (string.Equals(appId, "qrmenu-web", StringComparison.OrdinalIgnoreCase)) {
            return resetPath.StartsWith("/menu/", StringComparison.Ordinal)
                && resetPath.EndsWith("/reset-password", StringComparison.Ordinal)
                && !resetPath.Contains("//", StringComparison.Ordinal);
        }

        if (string.Equals(appId, "public-web", StringComparison.OrdinalIgnoreCase)
            || string.Equals(appId, "continuo-web", StringComparison.OrdinalIgnoreCase)) {
            return string.Equals(resetPath, "/web/reset-password", StringComparison.Ordinal);
        }

        return false;
    }

    private static string? TryResolveHost(string? origin) {
        if (string.IsNullOrWhiteSpace(origin) || !Uri.TryCreate(origin.Trim(), UriKind.Absolute, out var uri)) {
            return null;
        }

        return uri.Host;
    }

    private static string? ResolveTenantKey(Tenant? tenant) {
        if (tenant == null) {
            return null;
        }

        var slug = NormalizeTenantKey(tenant.Slug);
        return !string.IsNullOrEmpty(slug) ? slug : NormalizeTenantKey(tenant.Code);
    }

    private static string? NormalizeTenantKey(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length <= max ? value.Trim() : value.Trim()[..max];

    private static Task UniformDelayAsync(CancellationToken ct) =>
        Task.Delay(Random.Shared.Next(80, 220), ct);
}
