namespace AuthApi.Contracts.Requests;

/// <summary>
/// Create a brand-new active version for a given <c>Code</c>. Auto-deactivates
/// previous active row. Used by continuo-ops-ui "Yeni Versiyon Yayınla" action.
/// </summary>
public record CreatePlatformAgreementRequest(
    string Code,
    string Title,
    string BodyMd,
    string Version,
    bool IsRequired,
    int SortOrder
);

/// <summary>
/// Update an existing row in-place (title, body, required, sort order). Used
/// for minor edits that don't bump version (typos, formatting). For breaking
/// content changes the operator should publish a new version instead.
/// </summary>
public record UpdatePlatformAgreementRequest(
    string Title,
    string BodyMd,
    bool IsRequired,
    int SortOrder
);
