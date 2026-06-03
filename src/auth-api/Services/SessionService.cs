using System.Security.Cryptography;
using AuthApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Services;

public interface ISessionService {
    /// <summary>
    /// Create a new session for the credential on the given UI app.
    /// If <paramref name="exemptFromSingleSession"/> is false, all other active
    /// sessions for the same (credential, appId) tuple are revoked with reason
    /// "displaced" — enforcing single-active-session per UI per user.
    /// Returns the persisted session plus the freshly generated opaque session
    /// token (caller's only chance to see the plaintext — only the hash is stored).
    /// </summary>
    Task<(UserSession Session, string OpaqueToken)> CreateAsync(
        Ulid credentialId,
        string appId,
        bool exemptFromSingleSession,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default);

    /// <summary>
    /// Looks up a session by id. Returns null if not found, expired, or revoked.
    /// Touches LastSeenAtUtc when found and active.
    /// </summary>
    Task<UserSession?> TouchActiveAsync(Ulid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Resolve an active session by its opaque token (the value the browser holds
    /// in its HttpOnly cookie). Returns null if the hash is unknown or the
    /// session has been revoked. Bumps LastSeenAtUtc when a live row is found.
    /// </summary>
    Task<UserSession?> ResolveByOpaqueTokenAsync(string opaqueToken, CancellationToken ct = default);

    Task RevokeAsync(Ulid sessionId, string reason, CancellationToken ct = default);

    /// <summary>
    /// Revoke a session by the opaque token the caller holds. Idempotent — if the
    /// token is unknown or already revoked the method returns without error.
    /// </summary>
    Task RevokeByOpaqueTokenAsync(string opaqueToken, string reason, CancellationToken ct = default);
}

public class SessionService : ISessionService {
    private readonly AuthDbContext _db;
    private readonly ILogger<SessionService> _logger;

    public SessionService(AuthDbContext db, ILogger<SessionService> logger) {
        _db = db;
        _logger = logger;
    }

    public async Task<(UserSession Session, string OpaqueToken)> CreateAsync(
        Ulid credentialId,
        string appId,
        bool exemptFromSingleSession,
        string? ipAddress,
        string? userAgent,
        CancellationToken ct = default) {
        if (string.IsNullOrWhiteSpace(appId)) {
            throw new ArgumentException("appId is required", nameof(appId));
        }

        var normalizedAppId = appId.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;

        if (!exemptFromSingleSession) {
            // Displace any other active session for this credential+app combination.
            // platform.owner kullanıcıları bu kuraldan muaf — birden fazla cihazdan
            // eş zamanlı oturum açabilir (support/impersonation senaryosu için).
            var active = await _db.UserSessions
                .Where(s => s.CredentialId == credentialId
                    && s.AppId == normalizedAppId
                    && s.RevokedAtUtc == null)
                .ToListAsync(ct);

            foreach (var s in active) {
                s.RevokedAtUtc = now;
                s.RevokedReason = UserSessionRevocationReasons.Displaced;
            }

            if (active.Count > 0) {
                _logger.LogInformation(
                    "Displaced {Count} active session(s) for credential {CredentialId} on app {AppId}",
                    active.Count, credentialId, normalizedAppId);
            }
        }

        var opaqueToken = GenerateOpaqueToken();
        var session = new UserSession {
            Id = Ulid.NewUlid(),
            CredentialId = credentialId,
            AppId = normalizedAppId,
            CreatedAtUtc = now,
            LastSeenAtUtc = now,
            IpAddress = TruncateForColumn(ipAddress, 64),
            UserAgent = TruncateForColumn(userAgent, 512),
            SessionTokenHash = HashOpaqueToken(opaqueToken)
        };

        _db.UserSessions.Add(session);
        await _db.SaveChangesAsync(ct);
        return (session, opaqueToken);
    }

    public async Task<UserSession?> TouchActiveAsync(Ulid sessionId, CancellationToken ct = default) {
        var session = await _db.UserSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session == null || session.RevokedAtUtc != null) {
            return null;
        }

        session.LastSeenAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return session;
    }

    public async Task<UserSession?> ResolveByOpaqueTokenAsync(string opaqueToken, CancellationToken ct = default) {
        if (string.IsNullOrWhiteSpace(opaqueToken)) {
            return null;
        }

        var hash = HashOpaqueToken(opaqueToken);
        var session = await _db.UserSessions
            .FirstOrDefaultAsync(s => s.SessionTokenHash == hash, ct);

        if (session == null || session.RevokedAtUtc != null) {
            return null;
        }

        session.LastSeenAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return session;
    }

    public async Task RevokeAsync(Ulid sessionId, string reason, CancellationToken ct = default) {
        var session = await _db.UserSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session == null || session.RevokedAtUtc != null) {
            return;
        }

        session.RevokedAtUtc = DateTime.UtcNow;
        session.RevokedReason = reason;
        await _db.SaveChangesAsync(ct);
    }

    public async Task RevokeByOpaqueTokenAsync(string opaqueToken, string reason, CancellationToken ct = default) {
        if (string.IsNullOrWhiteSpace(opaqueToken)) {
            return;
        }

        var hash = HashOpaqueToken(opaqueToken);
        var session = await _db.UserSessions
            .FirstOrDefaultAsync(s => s.SessionTokenHash == hash, ct);
        if (session == null || session.RevokedAtUtc != null) {
            return;
        }

        session.RevokedAtUtc = DateTime.UtcNow;
        session.RevokedReason = reason;
        await _db.SaveChangesAsync(ct);
    }

    private static string? TruncateForColumn(string? value, int max) {
        if (string.IsNullOrWhiteSpace(value)) {
            return null;
        }
        return value.Length <= max ? value : value[..max];
    }

    // 32 random bytes → ~43 char base64url string. Fits any cookie limit with
    // room to spare and is too long to brute-force.
    private static string GenerateOpaqueToken() {
        var buffer = new byte[32];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    // SHA-256(base64) — 44 chars. Indexed lookup on equality is O(log n).
    // Storing only the hash means a DB dump never leaks live session tokens.
    internal static string HashOpaqueToken(string opaqueToken) {
        var bytes = System.Text.Encoding.UTF8.GetBytes(opaqueToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }
}
