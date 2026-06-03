using AuthApi.Data;
using AuthApi.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Continuo.Shared.Contracts;

namespace AuthApi.Consumers;

/// <summary>
/// Consumes <see cref="TenantOwnerSeedRequestedEvent"/> from tenant-api after a
/// new tenant is approved + provisioned. Creates the AuthDb Tenant row (if
/// missing) + the first owner TenantUser + Credential with the supplied temp
/// password (BCrypt-hashed, <c>MustChangePassword=true</c>). Plaintext password
/// from the event is never persisted — only the hash.
/// <para>
/// Idempotent: re-receiving the same event for an existing (tenant slug,
/// active email) pair is a no-op (tenant row preserved, credential password
/// NOT rotated to avoid surprising the owner). Use <c>POST .../reset-password</c>
/// for explicit rotation.
/// </para>
/// </summary>
public sealed class TenantOwnerSeedRequestedConsumer : IConsumer<TenantOwnerSeedRequestedEvent> {
    private readonly AuthDbContext _db;
    private readonly ILogger<TenantOwnerSeedRequestedConsumer> _log;

    public TenantOwnerSeedRequestedConsumer(AuthDbContext db, ILogger<TenantOwnerSeedRequestedConsumer> log) {
        _db = db;
        _log = log;
    }

    public async Task Consume(ConsumeContext<TenantOwnerSeedRequestedEvent> context) {
        var msg = context.Message;
        var ct = context.CancellationToken;

        if (string.IsNullOrWhiteSpace(msg.TenantSlug) ||
            string.IsNullOrWhiteSpace(msg.OwnerEmail) ||
            string.IsNullOrWhiteSpace(msg.TempPassword)) {
            _log.LogWarning("TenantOwnerSeed: missing slug/email/password — skipping. requestId={RequestId}", msg.RequestId);
            return;
        }

        var slug = msg.TenantSlug.Trim().ToLowerInvariant();
        var email = msg.OwnerEmail.Trim().ToLowerInvariant();

        // 1) Find or create Tenant row.
        var tenant = await _db.Tenants.FirstOrDefaultAsync(
            t => t.Slug == slug || t.Code == slug || t.Subdomain == slug, ct);

        if (tenant is null) {
            tenant = new Tenant {
                Code = slug,
                Name = msg.TenantDisplayName,
                Slug = slug,
                Subdomain = slug,
                ContactEmail = email,
                ContactPhone = msg.ContactPhone,
                Status = TenantStatus.Active
            };
            _db.Tenants.Add(tenant);
            await _db.SaveChangesAsync(ct);
            _log.LogInformation("TenantOwnerSeed: created Tenant {Slug} (id={Id})", slug, tenant.Id);
        }
        else {
            _log.LogInformation("TenantOwnerSeed: Tenant {Slug} already exists (id={Id})", slug, tenant.Id);
        }

        // 2) Idempotency — active TenantUser with same email in this tenant means we already seeded.
        var existingUser = await _db.TenantUsers
            .Include(u => u.Credentials)
            .FirstOrDefaultAsync(u =>
                u.TenantId == tenant.Id &&
                u.Email == email &&
                u.Status == TenantUserStatus.Active, ct);

        if (existingUser is not null) {
            _log.LogInformation(
                "TenantOwnerSeed: owner already exists for tenant {Slug} (userId={UserId}) — skipping create.",
                slug, existingUser.Id);
            return;
        }

        // 3) Reuse existing inactive user OR create fresh.
        var inactive = await _db.TenantUsers
            .Include(u => u.Credentials)
            .Where(u =>
                u.TenantId == tenant.Id &&
                u.Email == email &&
                u.Status != TenantUserStatus.Active &&
                u.Status != TenantUserStatus.Deleted)
            .OrderByDescending(u => u.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        TenantUser user;
        if (inactive is not null) {
            inactive.Status = TenantUserStatus.Active;
            inactive.DisplayName = msg.OwnerDisplayName;
            inactive.UpdatedAtUtc = DateTime.UtcNow;
            user = inactive;
        }
        else {
            user = new TenantUser {
                TenantId = tenant.Id,
                Email = email,
                DisplayName = msg.OwnerDisplayName,
                PhoneNumber = msg.ContactPhone,
                Status = TenantUserStatus.Active,
                PositionTitle = "Sahibi"
            };
            _db.TenantUsers.Add(user);
        }

        // 4) Credential — reuse active one across same-login or create new.
        var credential = await _db.Credentials.FirstOrDefaultAsync(c =>
            c.Login == email && c.IsActive, ct);

        if (credential is null) {
            credential = new Credential {
                Login = email,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(msg.TempPassword),
                OwnerType = CredentialOwnerType.TenantUser,
                TenantUser = user,
                IsActive = true,
                IsPrimary = true,
                MustChangePassword = true
            };
            _db.Credentials.Add(credential);
        }
        else {
            // Existing credential — only relink if not already linked here.
            if (credential.TenantUserId == null || credential.TenantUserId == user.Id) {
                credential.TenantUser = user;
                credential.PasswordHash = BCrypt.Net.BCrypt.HashPassword(msg.TempPassword);
                credential.MustChangePassword = true;
                credential.PasswordChangedAtUtc = null;
                if (!string.IsNullOrWhiteSpace(credential.Email) == false) {
                    credential.Email = email;
                }
            }
            else {
                _log.LogWarning(
                    "TenantOwnerSeed: credential {Login} already linked to another TenantUser ({Other}); " +
                    "leaving as-is for safety. Manual review needed.",
                    email, credential.TenantUserId);
            }
        }

        await _db.SaveChangesAsync(ct);

        _log.LogInformation(
            "TenantOwnerSeed: owner provisioned for tenant {Slug} → userId={UserId} email={Email} (must-change-password)",
            slug, user.Id, email);
    }
}
