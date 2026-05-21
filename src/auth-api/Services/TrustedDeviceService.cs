using System.Security.Cryptography;
using System.Text;
using AuthApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthApi.Services;

public interface ITrustedDeviceService {
    Task<bool> IsTrustedAsync(Ulid credentialId, string? rawToken, CancellationToken ct);
    Task<string> IssueAsync(Ulid credentialId, string? userAgent, string? ipAddress, CancellationToken ct);
}

public class TrustedDeviceService : ITrustedDeviceService {
    private readonly AuthDbContext _db;
    private readonly IOptions<TwoFactorOptions> _options;
    private readonly ILogger<TrustedDeviceService> _logger;

    public TrustedDeviceService(AuthDbContext db, IOptions<TwoFactorOptions> options, ILogger<TrustedDeviceService> logger) {
        _db = db;
        _options = options;
        _logger = logger;
    }

    public async Task<bool> IsTrustedAsync(Ulid credentialId, string? rawToken, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(rawToken)) {
            return false;
        }

        var hash = HashToken(rawToken);
        var now = DateTime.UtcNow;

        var trust = await _db.TrustedDevices
            .FirstOrDefaultAsync(
                x => x.CredentialId == credentialId
                  && x.TokenHash == hash
                  && x.RevokedAtUtc == null
                  && x.ExpiresAtUtc > now,
                ct);

        if (trust == null) {
            return false;
        }

        // Slide the last-used timestamp so stale entries can be reaped later
        // by an offline job. Expiry is not extended — trust still ends at the
        // originally issued ExpiresAtUtc.
        trust.LastUsedAtUtc = now;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<string> IssueAsync(Ulid credentialId, string? userAgent, string? ipAddress, CancellationToken ct) {
        var rawToken = GenerateRawToken();
        var now = DateTime.UtcNow;
        var ttl = TimeSpan.FromDays(Math.Max(1, _options.Value.TrustedDeviceTtlDays));

        var trust = new TrustedDevice {
            CredentialId = credentialId,
            TokenHash = HashToken(rawToken),
            UserAgent = Truncate(userAgent, 512),
            IpAddress = Truncate(ipAddress, 64),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(ttl),
            LastUsedAtUtc = now
        };

        _db.TrustedDevices.Add(trust);
        await _db.SaveChangesAsync(ct);

        if (_logger.IsEnabled(LogLevel.Information)) {
            _logger.LogInformation(
                "Issued trusted-device token {TrustId} for credential {CredentialId} (expires {Expires:o})",
                trust.Id, credentialId, trust.ExpiresAtUtc);
        }

        return rawToken;
    }

    private static string GenerateRawToken() {
        Span<byte> buffer = stackalloc byte[32];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string HashToken(string rawToken) {
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private static string? Truncate(string? value, int max) {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length > max ? value[..max] : value;
    }
}
