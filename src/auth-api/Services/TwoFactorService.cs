using System.Security.Cryptography;
using AuthApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AuthApi.Services;

public class TwoFactorService {
    private readonly AuthDbContext _db;
    private readonly ITwoFactorNotifier _notifier;
    private readonly IOptions<TwoFactorOptions> _options;
    private readonly ILogger<TwoFactorService> _logger;

    public TwoFactorService(AuthDbContext db, ITwoFactorNotifier notifier, IOptions<TwoFactorOptions> options, ILogger<TwoFactorService> logger) {
        _db = db;
        _notifier = notifier;
        _options = options;
        _logger = logger;
    }

    public bool RequiresTwoFactor(Credential credential) {
        if (!_options.Value.Enabled) {
            return false;
        }

        // Bootstrap bypass — credentials in the whitelist always skip 2FA.
        // Used for the seeded platform.owner on first deploy when no email/SMS
        // dispatch channel is configured yet, or as break-glass admin access.
        var bypass = _options.Value.BypassEmails;
        if (bypass != null && bypass.Length > 0 && !string.IsNullOrWhiteSpace(credential.Login)) {
            if (bypass.Any(e => string.Equals(e, credential.Login, StringComparison.OrdinalIgnoreCase))) {
                _logger.LogInformation(
                    "2FA bypassed for bootstrap credential {Login} (TwoFactor__BypassEmails)",
                    credential.Login);
                return false;
            }
        }

        var ownerType = credential.OwnerType.ToString();
        if (_options.Value.RequiredOwnerTypes == null || _options.Value.RequiredOwnerTypes.Length == 0) {
            return true;
        }

        return _options.Value.RequiredOwnerTypes.Any(o => string.Equals(o, ownerType, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<TwoFactorChallenge> CreateChallengeAsync(Credential credential, CancellationToken ct) {
        var channel = ResolveChannel();
        var target = ResolveTarget(credential);
        var now = DateTime.UtcNow;

        var activeChallenge = await _db.TwoFactorChallenges
            .Where(x => x.CredentialId == credential.Id
                && x.VerifiedAtUtc == null
                && x.ExpiresAtUtc > now
                && x.Channel == channel
                && x.Target == target
                && x.LastError == null)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (activeChallenge != null) {
            if (_logger.IsEnabled(LogLevel.Information)) {
                _logger.LogInformation(
                    "Reusing active 2FA challenge {ChallengeId} for credential {Credential}",
                    activeChallenge.Id,
                    credential.Login);
            }
            return activeChallenge;
        }

        var code = GenerateCode(_options.Value.CodeLength);

        var challenge = new TwoFactorChallenge {
            CredentialId = credential.Id,
            Credential = credential,
            Channel = channel,
            Target = target,
            CodeHash = BCrypt.Net.BCrypt.HashPassword(code),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(_options.Value.CodeTtlMinutes)
        };

        _db.TwoFactorChallenges.Add(challenge);
        await _db.SaveChangesAsync(ct);

        try {
            await _notifier.NotifyAsync(
                new TwoFactorDispatchPayload(challenge.Id.ToString(), channel, target, code, challenge.ExpiresAtUtc, ResolveDisplayName(credential), ResolveTenantName(credential), _options.Value.FlowName),
                ct);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to dispatch two factor notification for credential {Credential}", credential.Login);
            challenge.LastError = "DispatchFailed";
            await _db.SaveChangesAsync(ct);
            throw;
        }

        return challenge;
    }

    public async Task<TwoFactorResendResult> ResendChallengeAsync(string challengeId, CancellationToken ct) {
        if (!Ulid.TryParse(challengeId, out var parsedId)) {
            return TwoFactorResendResult.Invalid("InvalidChallengeId");
        }

        var challenge = await _db.TwoFactorChallenges
            .Include(x => x.Credential)
                .ThenInclude(c => c.PlatformUser)
            .Include(x => x.Credential)
                .ThenInclude(c => c.TenantUser)
                    .ThenInclude(u => u!.Tenant)
            .Include(x => x.Credential)
                .ThenInclude(c => c.Customer)
                    .ThenInclude(cust => cust!.Tenant)
            .FirstOrDefaultAsync(x => x.Id == parsedId, ct);

        if (challenge == null || challenge.Credential == null) {
            return TwoFactorResendResult.Invalid("ChallengeNotFound");
        }

        if (challenge.IsVerified) {
            return TwoFactorResendResult.Invalid("ChallengeAlreadyUsed");
        }

        var now = DateTime.UtcNow;
        var cooldownSec = Math.Max(0, _options.Value.ResendCooldownSeconds);
        var lastSentAt = challenge.LastResendAtUtc ?? challenge.CreatedAtUtc;
        var nextAllowedAt = lastSentAt.AddSeconds(cooldownSec);
        if (now < nextAllowedAt) {
            return TwoFactorResendResult.RateLimited(nextAllowedAt);
        }

        var code = GenerateCode(_options.Value.CodeLength);
        challenge.CodeHash = BCrypt.Net.BCrypt.HashPassword(code);
        challenge.ExpiresAtUtc = now.AddMinutes(_options.Value.CodeTtlMinutes);
        challenge.LastResendAtUtc = now;
        challenge.FailedAttempts = 0;
        challenge.LastError = null;
        await _db.SaveChangesAsync(ct);

        try {
            await _notifier.NotifyAsync(
                new TwoFactorDispatchPayload(
                    challenge.Id.ToString(),
                    challenge.Channel,
                    challenge.Target,
                    code,
                    challenge.ExpiresAtUtc,
                    ResolveDisplayName(challenge.Credential),
                    ResolveTenantName(challenge.Credential),
                    _options.Value.FlowName),
                ct);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Failed to dispatch resend two factor notification for challenge {ChallengeId}", challenge.Id);
            challenge.LastError = "DispatchFailed";
            await _db.SaveChangesAsync(ct);
            return TwoFactorResendResult.Invalid("DispatchFailed");
        }

        return TwoFactorResendResult.Ok(challenge.ExpiresAtUtc);
    }

    public async Task<TwoFactorVerificationResult> VerifyAsync(string challengeId, string code, CancellationToken ct) {
        if (!Ulid.TryParse(challengeId, out var parsedId)) {
            return TwoFactorVerificationResult.Invalid("InvalidChallengeId");
        }

        var challenge = await _db.TwoFactorChallenges
            .Include(x => x.Credential)
                .ThenInclude(c => c.PlatformUser)
                    .ThenInclude(u => u!.Roles)
                        .ThenInclude(r => r.Role!)
                            .ThenInclude(role => role!.Permissions)
            .Include(x => x.Credential)
                .ThenInclude(c => c.TenantUser)
                    .ThenInclude(u => u!.Tenant)
            .Include(x => x.Credential)
                .ThenInclude(c => c.TenantUser)
                    .ThenInclude(u => u!.Roles)
                        .ThenInclude(r => r.Role!)
                            .ThenInclude(role => role!.Permissions)
            .Include(x => x.Credential)
                .ThenInclude(c => c.Customer)
                    .ThenInclude(cust => cust!.Tenant)
            .FirstOrDefaultAsync(x => x.Id == parsedId, ct);

        if (challenge == null || challenge.Credential == null) {
            return TwoFactorVerificationResult.Invalid("ChallengeNotFound");
        }

        if (challenge.IsVerified) {
            return TwoFactorVerificationResult.Invalid("ChallengeAlreadyUsed");
        }

        var now = DateTime.UtcNow;
        if (challenge.IsExpired(now)) {
            challenge.LastError = "Expired";
            await _db.SaveChangesAsync(ct);
            return TwoFactorVerificationResult.Invalid("ChallengeExpired");
        }

        var valid = BCrypt.Net.BCrypt.Verify(code, challenge.CodeHash);
        if (!valid) {
            challenge.FailedAttempts += 1;
            challenge.LastError = "InvalidCode";
            if (challenge.FailedAttempts >= _options.Value.MaxAttempts) {
                challenge.VerifiedAtUtc = now;
            }
            await _db.SaveChangesAsync(ct);
            return TwoFactorVerificationResult.Invalid("CodeMismatch");
        }

        challenge.VerifiedAtUtc = now;
        challenge.CodeHash = string.Empty;
        challenge.LastError = null;
        challenge.Credential.LastLoginAtUtc = now;
        await _db.SaveChangesAsync(ct);

        return TwoFactorVerificationResult.FromCredential(challenge.Credential);
    }

    private static string ResolveDisplayName(Credential credential) {
        return credential.OwnerType switch {
            CredentialOwnerType.PlatformUser => credential.PlatformUser?.DisplayName ?? credential.Login,
            CredentialOwnerType.TenantUser => credential.TenantUser?.DisplayName ?? credential.Login,
            CredentialOwnerType.Customer => credential.Customer?.DisplayName ?? credential.Login,
            _ => credential.Login
        };
    }

    private static string? ResolveTenantName(Credential credential) {
        if (credential.OwnerType == CredentialOwnerType.TenantUser) {
            return credential.TenantUser?.Tenant?.Name;
        }

        if (credential.OwnerType == CredentialOwnerType.Customer) {
            return credential.Customer?.Tenant?.Name;
        }

        return null;
    }

    private string ResolveChannel() => string.IsNullOrWhiteSpace(_options.Value.PreferredChannel) ? "Email" : _options.Value.PreferredChannel;

    private static string ResolveTarget(Credential credential) {
        if (!string.IsNullOrWhiteSpace(credential.Email)) {
            return credential.Email;
        }

        return credential.OwnerType switch {
            CredentialOwnerType.PlatformUser => credential.PlatformUser?.Email ?? credential.Login,
            CredentialOwnerType.TenantUser => credential.TenantUser?.Email ?? credential.Login,
            CredentialOwnerType.Customer => credential.Customer?.Email ?? credential.Login,
            _ => credential.Login
        };
    }

    private static string GenerateCode(int length) {
        length = Math.Clamp(length, 4, 8);
        Span<byte> buffer = stackalloc byte[4];
        RandomNumberGenerator.Fill(buffer);
        var value = BitConverter.ToUInt32(buffer);
        var max = (int)Math.Pow(10, length);
        var code = (value % max).ToString($"D{length}");
        return code;
    }
}
