using AuthApi.Models.PlatformSettings;

namespace AuthApi.Services.PlatformSettings;

/// <summary>
/// Read/write the runtime branding/identity surface. Storage is the generic
/// <c>ParameterDefinitions</c> table (module=platform, section=branding).
/// </summary>
public interface IPlatformSettingsService {
    /// <summary>
    /// Resolve current settings. When <paramref name="tenantCode"/> is set,
    /// overlay tenant-scoped rows (only for the 5 overrideable keys) on top
    /// of platform-scoped defaults. Missing rows fall back to
    /// <see cref="PlatformBrandingSettingsDto.Defaults"/>.
    /// </summary>
    Task<PlatformBrandingSettingsDto> ResolveAsync(string? tenantCode, CancellationToken ct);

    /// <summary>
    /// Upsert the provided non-null fields. <paramref name="tenantCode"/> null =
    /// platform scope; non-null = tenant scope (rejects writes for the 2
    /// platform-only keys via <see cref="UpdateScopeViolationException"/>).
    /// </summary>
    Task UpdateAsync(string? tenantCode, PlatformBrandingSettingsDto dto, string updatedBy, CancellationToken ct);
}

public sealed class UpdateScopeViolationException : Exception {
    public UpdateScopeViolationException(string message) : base(message) { }
}
