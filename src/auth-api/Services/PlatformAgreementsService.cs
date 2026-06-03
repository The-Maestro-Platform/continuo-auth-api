using AuthApi.Contracts.Requests;
using AuthApi.Contracts.Responses;
using AuthApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Services;

public class PlatformAgreementsService {
    private readonly AuthDbContext _db;
    private readonly PlatformIdentityService _identity;

    public PlatformAgreementsService(AuthDbContext db, PlatformIdentityService identity) {
        _db = db;
        _identity = identity;
    }

    /// <summary>Public — anon read of the current active agreement set,
    /// ordered by SortOrder. Bodies are <b>token-resolved</b> against the
    /// current platform identity before returning (qrmenu-mobile / qrmenu-web
    /// never see raw <c>{{companyName}}</c>).</summary>
    public async Task<List<PlatformAgreementResponse>> ListActiveAsync(CancellationToken ct) {
        var rows = await _db.PlatformAgreements
            .AsNoTracking()
            .Where(a => a.IsActive)
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Code)
            .ToListAsync(ct);
        if (rows.Count == 0) return new List<PlatformAgreementResponse>();
        var identity = await _identity.GetEntityAsync(ct);
        return rows.Select(r => ToResponse(r, identity)).ToList();
    }


    /// <summary>Admin — full list, includes inactive history rows.</summary>
    public async Task<List<PlatformAgreementResponse>> ListAllAsync(CancellationToken ct) {
        var rows = await _db.PlatformAgreements
            .AsNoTracking()
            .OrderBy(a => a.Code)
            .ThenByDescending(a => a.EffectiveFromUtc)
            .ToListAsync(ct);
        return rows.Select(ToResponse).ToList();
    }

    public async Task<PlatformAgreementResponse?> GetByIdAsync(Ulid id, CancellationToken ct) {
        var row = await _db.PlatformAgreements
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        return row is null ? null : ToResponse(row);
    }

    /// <summary>Create a new active version for the given Code. Atomically
    /// deactivates the previous active row, so the Code/IsActive=1 uniqueness
    /// stays at 1. Throws on (Code, Version) collision.</summary>
    public async Task<PlatformAgreementResponse> CreateAsync(
        CreatePlatformAgreementRequest req,
        string? updatedBy,
        CancellationToken ct
    ) {
        var code = NormalizeCode(req.Code);
        var version = NormalizeVersion(req.Version);
        var title = (req.Title ?? string.Empty).Trim();
        var body = req.BodyMd ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Code is required.", nameof(req));
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(req));
        if (string.IsNullOrWhiteSpace(version)) throw new ArgumentException("Version is required.", nameof(req));
        if (string.IsNullOrWhiteSpace(body)) throw new ArgumentException("Body is required.", nameof(req));

        var duplicate = await _db.PlatformAgreements
            .AnyAsync(a => a.Code == code && a.Version == version, ct);
        if (duplicate) {
            throw new InvalidOperationException($"An agreement with Code={code} Version={version} already exists.");
        }

        using var tx = await _db.Database.BeginTransactionAsync(ct);
        var previous = await _db.PlatformAgreements
            .Where(a => a.Code == code && a.IsActive)
            .ToListAsync(ct);
        foreach (var p in previous) {
            p.IsActive = false;
            p.UpdatedAtUtc = DateTime.UtcNow;
            p.UpdatedBy = updatedBy;
        }

        var entity = new PlatformAgreement {
            Id = Ulid.NewUlid(),
            Code = code,
            Title = title,
            BodyMd = body,
            Version = version,
            EffectiveFromUtc = DateTime.UtcNow,
            IsActive = true,
            IsRequired = req.IsRequired,
            SortOrder = req.SortOrder,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            UpdatedBy = updatedBy
        };
        _db.PlatformAgreements.Add(entity);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToResponse(entity);
    }

    /// <summary>In-place edit of an existing row — no version bump. For
    /// content changes that don't need re-consent. Code/Version/IsActive
    /// stay untouched.</summary>
    public async Task<PlatformAgreementResponse?> UpdateAsync(
        Ulid id,
        UpdatePlatformAgreementRequest req,
        string? updatedBy,
        CancellationToken ct
    ) {
        var row = await _db.PlatformAgreements.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (row is null) return null;

        var title = (req.Title ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(req));
        if (string.IsNullOrWhiteSpace(req.BodyMd)) throw new ArgumentException("Body is required.", nameof(req));

        row.Title = title;
        row.BodyMd = req.BodyMd;
        row.IsRequired = req.IsRequired;
        row.SortOrder = req.SortOrder;
        row.UpdatedAtUtc = DateTime.UtcNow;
        row.UpdatedBy = updatedBy;
        await _db.SaveChangesAsync(ct);
        return ToResponse(row);
    }

    /// <summary>Activate a historical version — atomically deactivates the
    /// current active row for the same Code. Used to roll back a botched
    /// publish.</summary>
    public async Task<PlatformAgreementResponse?> ActivateAsync(Ulid id, string? updatedBy, CancellationToken ct) {
        var target = await _db.PlatformAgreements.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (target is null) return null;
        if (target.IsActive) return ToResponse(target);

        using var tx = await _db.Database.BeginTransactionAsync(ct);
        var others = await _db.PlatformAgreements
            .Where(a => a.Code == target.Code && a.IsActive && a.Id != target.Id)
            .ToListAsync(ct);
        foreach (var o in others) {
            o.IsActive = false;
            o.UpdatedAtUtc = DateTime.UtcNow;
            o.UpdatedBy = updatedBy;
        }
        target.IsActive = true;
        target.UpdatedAtUtc = DateTime.UtcNow;
        target.UpdatedBy = updatedBy;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToResponse(target);
    }

    /// <summary>Delete an inactive historical row. Active rows cannot be
    /// deleted — caller must publish a new version first (which deactivates
    /// the old one) and then delete the inactive copy.</summary>
    public async Task<bool> DeleteAsync(Ulid id, CancellationToken ct) {
        var row = await _db.PlatformAgreements.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (row is null) return false;
        if (row.IsActive) {
            throw new InvalidOperationException("Active agreement cannot be deleted. Publish a new version first.");
        }
        _db.PlatformAgreements.Remove(row);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static string NormalizeCode(string? code) =>
        (code ?? string.Empty).Trim().ToLowerInvariant();

    private static string NormalizeVersion(string? version) =>
        (version ?? string.Empty).Trim();

    // Raw body (admin endpoints) — editor needs to see {{tokens}} as-is so
    // they can be edited; publishing a new version keeps the tokens intact.
    private static PlatformAgreementResponse ToResponse(PlatformAgreement a) =>
        ToResponseCore(a, a.BodyMd);

    // Resolved body (public endpoint) — render tokens against the supplied
    // identity + per-agreement metadata so mobile/web sees the final text.
    private static PlatformAgreementResponse ToResponse(PlatformAgreement a, PlatformIdentity identity) {
        var meta = new AgreementRenderMetadata(a.Title, a.Version, a.EffectiveFromUtc);
        var rendered = PlatformIdentityRenderer.Render(a.BodyMd, identity, meta);
        return ToResponseCore(a, rendered);
    }

    private static PlatformAgreementResponse ToResponseCore(PlatformAgreement a, string body) =>
        new(
            a.Id.ToString(),
            a.Code,
            a.Title,
            body,
            a.Version,
            DateTime.SpecifyKind(a.EffectiveFromUtc, DateTimeKind.Utc),
            a.IsActive,
            a.IsRequired,
            a.SortOrder,
            DateTime.SpecifyKind(a.CreatedAtUtc, DateTimeKind.Utc),
            DateTime.SpecifyKind(a.UpdatedAtUtc, DateTimeKind.Utc),
            a.UpdatedBy
        );
}
