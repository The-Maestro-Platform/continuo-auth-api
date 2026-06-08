namespace AuthApi.Contracts.Responses;

/// <summary>Admin (continuo-ops-ui) row + public (qrmenu-mobile / qrmenu-web) row.
/// Same shape — admin lists also include inactive history rows.</summary>
public record PlatformAgreementResponse(
    string Id,
    string Code,
    string Title,
    string BodyMd,
    string Version,
    DateTime EffectiveFromUtc,
    bool IsActive,
    bool IsRequired,
    int SortOrder,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string? UpdatedBy
);
