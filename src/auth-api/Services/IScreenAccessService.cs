using AuthApi.Models;

namespace AuthApi.Services;

public interface IScreenAccessService {
    Task<IReadOnlyList<string>> ResolveScreensAsync(
        Credential credential,
        IEnumerable<string> permissions,
        IEnumerable<Role> roles,
        string? appCode = null,
        CancellationToken ct = default);
}
