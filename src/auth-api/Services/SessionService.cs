using AuthApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Services;

public interface ISessionService {
    /// <summary>
    /// Create a new session for the credential on the given UI app.
    /// If <paramref name="exemptFromSingleSession"/> is false, all other active
    /// sessions for the same (credential, appId) tuple are revoked with reason
    /// "displaced" — enforcing single-active-session per UI per user.
    /// </summary>
    Task<UserSession> CreateAsync(
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

    Task RevokeAsync(Ulid sessionId, string reason, CancellationToken ct = default);
}

public class SessionService : ISessionService {
    private readonly AuthDbContext _db;
    private readonly ILogger<SessionService> _logger;

    public SessionService(AuthDbContext db, ILogger<SessionService> logger) {
        _db = db;
        _logger = logger;
    }

    public async Task<UserSession> CreateAsync(
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

        var session = new UserSession {
            Id = Ulid.NewUlid(),
            CredentialId = credentialId,
            AppId = normalizedAppId,
            CreatedAtUtc = now,
            LastSeenAtUtc = now,
            IpAddress = TruncateForColumn(ipAddress, 64),
            UserAgent = TruncateForColumn(userAgent, 512)
        };

        _db.UserSessions.Add(session);
        await _db.SaveChangesAsync(ct);
        return session;
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

    public async Task RevokeAsync(Ulid sessionId, string reason, CancellationToken ct = default) {
        var session = await _db.UserSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct);
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
}
