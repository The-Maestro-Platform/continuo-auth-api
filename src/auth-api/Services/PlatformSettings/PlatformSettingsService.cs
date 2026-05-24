using AuthApi.Models.PlatformSettings;
using Continuo.Persistence.Parameters;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Services.PlatformSettings;

public sealed class PlatformSettingsService : IPlatformSettingsService {
    private readonly AuthDbContext _db;

    public PlatformSettingsService(AuthDbContext db) {
        _db = db;
    }

    public async Task<PlatformBrandingSettingsDto> ResolveAsync(string? tenantCode, CancellationToken ct) {
        var normalizedTenant = NormalizeOrNull(tenantCode);

        var platformRows = await _db.ParameterDefinitions
            .AsNoTracking()
            .Where(p => p.Module == PlatformBrandingSettingsDto.Module
                        && p.Section == PlatformBrandingSettingsDto.Section
                        && p.TenantCode == null)
            .ToListAsync(ct);

        var tenantRows = normalizedTenant is null
            ? new List<ParameterDefinition>()
            : await _db.ParameterDefinitions
                .AsNoTracking()
                .Where(p => p.Module == PlatformBrandingSettingsDto.Module
                            && p.Section == PlatformBrandingSettingsDto.Section
                            && p.TenantCode == normalizedTenant)
                .ToListAsync(ct);

        string Resolve(string key, string fallback) {
            // Tenant overlay only for the 5 overrideable keys
            if (normalizedTenant is not null
                && PlatformBrandingSettingsDto.TenantOverrideableKeys.Contains(key)) {
                var tenantRow = tenantRows.FirstOrDefault(r => r.Key == key);
                if (tenantRow is not null && !string.IsNullOrEmpty(tenantRow.Value)) {
                    return tenantRow.Value;
                }
            }
            var platformRow = platformRows.FirstOrDefault(r => r.Key == key);
            return platformRow?.Value ?? fallback;
        }

        return new PlatformBrandingSettingsDto(
            BrandName: Resolve(PlatformBrandingSettingsDto.Keys.BrandName, PlatformBrandingSettingsDto.Defaults.BrandName),
            AssistantName: Resolve(PlatformBrandingSettingsDto.Keys.AssistantName, PlatformBrandingSettingsDto.Defaults.AssistantName),
            DomainHint: Resolve(PlatformBrandingSettingsDto.Keys.DomainHint, PlatformBrandingSettingsDto.Defaults.DomainHint),
            GithubRepo: Resolve(PlatformBrandingSettingsDto.Keys.GithubRepo, PlatformBrandingSettingsDto.Defaults.GithubRepo),
            UserAgent: Resolve(PlatformBrandingSettingsDto.Keys.UserAgent, PlatformBrandingSettingsDto.Defaults.UserAgent),
            ThemeAccentColor: Resolve(PlatformBrandingSettingsDto.Keys.ThemeAccentColor, PlatformBrandingSettingsDto.Defaults.ThemeAccentColor),
            LogoUrl: Resolve(PlatformBrandingSettingsDto.Keys.LogoUrl, PlatformBrandingSettingsDto.Defaults.LogoUrl));
    }

    public async Task UpdateAsync(string? tenantCode, PlatformBrandingSettingsDto dto, string updatedBy, CancellationToken ct) {
        var normalizedTenant = NormalizeOrNull(tenantCode);

        var keyValues = new Dictionary<string, string?> {
            [PlatformBrandingSettingsDto.Keys.BrandName] = dto.BrandName,
            [PlatformBrandingSettingsDto.Keys.AssistantName] = dto.AssistantName,
            [PlatformBrandingSettingsDto.Keys.DomainHint] = dto.DomainHint,
            [PlatformBrandingSettingsDto.Keys.GithubRepo] = dto.GithubRepo,
            [PlatformBrandingSettingsDto.Keys.UserAgent] = dto.UserAgent,
            [PlatformBrandingSettingsDto.Keys.ThemeAccentColor] = dto.ThemeAccentColor,
            [PlatformBrandingSettingsDto.Keys.LogoUrl] = dto.LogoUrl,
        };

        if (normalizedTenant is not null) {
            foreach (var key in keyValues.Keys.ToList()) {
                if (keyValues[key] is not null
                    && !PlatformBrandingSettingsDto.TenantOverrideableKeys.Contains(key)) {
                    throw new UpdateScopeViolationException(
                        $"Key '{key}' cannot be overridden at tenant scope.");
                }
            }
        }

        var existing = await _db.ParameterDefinitions
            .Where(p => p.Module == PlatformBrandingSettingsDto.Module
                        && p.Section == PlatformBrandingSettingsDto.Section
                        && p.TenantCode == normalizedTenant)
            .ToListAsync(ct);

        var existingByKey = existing.ToDictionary(p => p.Key);
        var now = DateTimeOffset.UtcNow;
        var changed = false;

        foreach (var (key, value) in keyValues) {
            if (value is null) continue; // partial update: skip null fields

            if (existingByKey.TryGetValue(key, out var row)) {
                if (row.Value == value) continue;
                row.Value = value;
                row.UpdatedAtUtc = now;
                row.UpdatedBy = updatedBy;
                row.Revision = Guid.NewGuid().ToString("N");
                changed = true;
            } else {
                _db.ParameterDefinitions.Add(new ParameterDefinition {
                    Id = Ulid.NewUlid(),
                    Module = PlatformBrandingSettingsDto.Module,
                    Section = PlatformBrandingSettingsDto.Section,
                    Key = key,
                    DataType = "string",
                    Scope = normalizedTenant is null ? "global" : "tenant",
                    Environment = "prod",
                    TenantCode = normalizedTenant,
                    Value = value,
                    UpdatedBy = updatedBy,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                    Revision = Guid.NewGuid().ToString("N"),
                });
                changed = true;
            }
        }

        if (changed) {
            await _db.SaveChangesAsync(ct);
        }
    }

    private static string? NormalizeOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}
