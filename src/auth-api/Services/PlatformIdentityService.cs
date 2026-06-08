using AuthApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace AuthApi.Services;

public record PlatformIdentityDto(
    string CompanyName,
    string CompanyLegalName,
    string CompanyAddress,
    string CompanyEmail,
    string CompanyKep,
    string CompanyPhone,
    string CompanyWebsite,
    string JurisdictionCity,
    DateTime UpdatedAtUtc,
    string? UpdatedBy
);

public record UpdatePlatformIdentityRequest(
    string CompanyName,
    string CompanyLegalName,
    string CompanyAddress,
    string CompanyEmail,
    string CompanyKep,
    string CompanyPhone,
    string CompanyWebsite,
    string JurisdictionCity
);

/// <summary>
/// Single-row identity reader/writer with 5-minute in-memory cache. The cache
/// is invalidated on update — agreements rendered after that call will see
/// the new values immediately. Mirrors the <c>PlatformSecretResolver</c>
/// pattern used elsewhere in auth-api.
/// </summary>
public class PlatformIdentityService {
    private const string CacheKey = "platform-identity::current";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly AuthDbContext _db;
    private readonly IMemoryCache _cache;

    public PlatformIdentityService(AuthDbContext db, IMemoryCache cache) {
        _db = db;
        _cache = cache;
    }

    public async Task<PlatformIdentity> GetEntityAsync(CancellationToken ct) {
        if (_cache.TryGetValue<PlatformIdentity>(CacheKey, out var cached) && cached is not null) {
            return cached;
        }
        var row = await _db.PlatformIdentities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.RowKey == "current", ct);
        // First-boot fallback — DDL bootstrap should have inserted defaults,
        // but if someone deleted the row we still return a usable shell so
        // token resolution does not crash.
        row ??= new PlatformIdentity {
            CompanyName = "Continuo",
            CompanyLegalName = "Continuo Bilişim Hizmetleri A.Ş.",
            CompanyAddress = "(adres continuo-ops-ui'dan doldurulacak)",
            CompanyEmail = "destek@example.local",
            CompanyKep = "continuo@hs01.kep.tr",
            CompanyPhone = "+90 850 000 00 00",
            CompanyWebsite = "www.example.local",
            JurisdictionCity = "İstanbul (Çağlayan)",
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedBy = "system:fallback"
        };
        _cache.Set(CacheKey, row, CacheTtl);
        return row;
    }

    public async Task<PlatformIdentityDto> GetAsync(CancellationToken ct) =>
        ToDto(await GetEntityAsync(ct));

    public async Task<PlatformIdentityDto> UpdateAsync(
        UpdatePlatformIdentityRequest req,
        string? updatedBy,
        CancellationToken ct
    ) {
        Validate(req);
        var row = await _db.PlatformIdentities.FirstOrDefaultAsync(x => x.RowKey == "current", ct);
        var created = row is null;
        if (created) {
            row = new PlatformIdentity { RowKey = "current" };
            _db.PlatformIdentities.Add(row);
        }
        row!.CompanyName = req.CompanyName.Trim();
        row.CompanyLegalName = req.CompanyLegalName.Trim();
        row.CompanyAddress = req.CompanyAddress.Trim();
        row.CompanyEmail = req.CompanyEmail.Trim();
        row.CompanyKep = (req.CompanyKep ?? string.Empty).Trim();
        row.CompanyPhone = (req.CompanyPhone ?? string.Empty).Trim();
        row.CompanyWebsite = (req.CompanyWebsite ?? string.Empty).Trim();
        row.JurisdictionCity = req.JurisdictionCity.Trim();
        row.UpdatedAtUtc = DateTime.UtcNow;
        row.UpdatedBy = updatedBy;
        await _db.SaveChangesAsync(ct);
        _cache.Remove(CacheKey); // invalidate so next read picks up the new row
        return ToDto(row);
    }

    private static void Validate(UpdatePlatformIdentityRequest req) {
        if (string.IsNullOrWhiteSpace(req.CompanyName))      throw new ArgumentException("CompanyName is required.");
        if (string.IsNullOrWhiteSpace(req.CompanyLegalName)) throw new ArgumentException("CompanyLegalName is required.");
        if (string.IsNullOrWhiteSpace(req.CompanyAddress))   throw new ArgumentException("CompanyAddress is required.");
        if (string.IsNullOrWhiteSpace(req.CompanyEmail))     throw new ArgumentException("CompanyEmail is required.");
        if (string.IsNullOrWhiteSpace(req.JurisdictionCity)) throw new ArgumentException("JurisdictionCity is required.");
    }

    private static PlatformIdentityDto ToDto(PlatformIdentity row) => new(
        row.CompanyName,
        row.CompanyLegalName,
        row.CompanyAddress,
        row.CompanyEmail,
        row.CompanyKep ?? string.Empty,
        row.CompanyPhone ?? string.Empty,
        row.CompanyWebsite ?? string.Empty,
        row.JurisdictionCity,
        DateTime.SpecifyKind(row.UpdatedAtUtc, DateTimeKind.Utc),
        row.UpdatedBy
    );
}
