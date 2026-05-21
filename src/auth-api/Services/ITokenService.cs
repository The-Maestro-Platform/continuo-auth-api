using AuthApi.Models;

namespace AuthApi.Services;

public interface ITokenService {
    Task<(string token, DateTime expires)> CreateAccessTokenAsync(Credential credential, IEnumerable<string> roles, IEnumerable<string> permissions, IEnumerable<string> screens, IEnumerable<string>? branchCodes = null, IEnumerable<string>? branchRoles = null, Ulid? sessionId = null);
    Task<(string token, DateTime expires)> CreateRefreshTokenAsync();
}
