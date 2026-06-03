using AuthApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuthApi.Services;

public class ScreenAccessService : IScreenAccessService {
    private readonly AuthDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<ScreenAccessService> _logger;

    public ScreenAccessService(AuthDbContext db, IConfiguration config, ILogger<ScreenAccessService> logger) {
        _db = db;
        _config = config;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> ResolveScreensAsync(
        Credential credential,
        IEnumerable<string> permissions,
        IEnumerable<Role> roles,
        string? appCode = null,
        CancellationToken ct = default) {
        var ownerLogin = _config["AUTH_OWNER_LOGIN"] ?? "platform.owner@example.local";
        var login = credential.Login?.ToLowerInvariant() ?? string.Empty;
        if (login == ownerLogin.ToLowerInvariant()) {
            _logger.LogInformation("User {Login} is owner, granting wildcard access", login);
            return new[] { "*" };
        }

        var permSet = permissions.Distinct(StringComparer.OrdinalIgnoreCase).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var roleIds = roles?.Select(r => r.Id).ToArray() ?? Array.Empty<Ulid>();

        _logger.LogInformation("ResolveScreens for {Login}, appCode={AppCode}, roleCount={RoleCount}, roleIds=[{RoleIds}]",
            login, appCode ?? "(all)", roleIds.Length, string.Join(",", roleIds));

        var screenQuery = _db.Screens.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(appCode)) {
            screenQuery = screenQuery.Where(s => s.AppCode == appCode);
        }

        var explicitScreens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var permissionlessScreens = await screenQuery
            .Where(s => string.IsNullOrWhiteSpace(s.RequiredPermissionsJson) || s.RequiredPermissionsJson == "[]")
            .Select(s => s.ScreenKey)
            .ToListAsync(ct);

        foreach (var key in permissionlessScreens) {
            explicitScreens.Add(key);
        }

        if (credential.OwnerType == CredentialOwnerType.PlatformUser && credential.PlatformUserId.HasValue) {
            var platformId = credential.PlatformUserId.Value;
            var now = DateTime.UtcNow;
            var screenIds = await _db.ScreenUsers.AsNoTracking()
                .Where(su => su.PlatformUserId == platformId && (su.ExpiresAtUtc == null || su.ExpiresAtUtc > now))
                .Select(su => su.ScreenId)
                .ToListAsync(ct);

            _logger.LogInformation("PlatformUser {Id}: found {Count} direct screen assignments", platformId, screenIds.Count);

            if (screenIds.Count > 0) {
                var assigned = await screenQuery.Where(s => screenIds.Contains(s.Id)).Select(s => s.ScreenKey).ToListAsync(ct);
                foreach (var key in assigned) {
                    explicitScreens.Add(key);
                }
            }
        }

        if (roleIds.Length > 0) {
            var roleScreenIds = await _db.ScreenRoles.AsNoTracking()
                .Where(sr => roleIds.Contains(sr.RoleId))
                .Select(sr => sr.ScreenId)
                .ToListAsync(ct);

            _logger.LogInformation("Role-based screens: found {Count} screen assignments for roles", roleScreenIds.Count);

            if (roleScreenIds.Count > 0) {
                var assigned = await screenQuery.Where(s => roleScreenIds.Contains(s.Id)).Select(s => s.ScreenKey).ToListAsync(ct);
                _logger.LogInformation("After appCode filter: {Count} screens - [{Screens}]", assigned.Count, string.Join(",", assigned));
                foreach (var key in assigned) {
                    explicitScreens.Add(key);
                }
            }
        }

        // Permission-based access: screens with RequiredPermissions that user has
        var permissionFiltered = await screenQuery
            .Where(s => !string.IsNullOrWhiteSpace(s.RequiredPermissionsJson) && s.RequiredPermissionsJson != "[]")
            .ToListAsync(ct);

        foreach (var screen in permissionFiltered) {
            var required = screen.RequiredPermissions;
            if (required.All(r => permSet.Contains(r))) {
                explicitScreens.Add(screen.ScreenKey);
            }
        }

        _logger.LogInformation("Final screens for {Login}: [{Screens}]", login, string.Join(",", explicitScreens));
        return explicitScreens.ToList();
    }
}
