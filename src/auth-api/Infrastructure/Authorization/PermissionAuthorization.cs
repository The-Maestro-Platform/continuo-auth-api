using Continuo.Shared.Security;

namespace AuthApi.Infrastructure.Authorization;

public static class PermissionAuthorization {
    public static bool HasAnyPermission(
        HttpContext? context,
        IConfiguration configuration,
        params string[] requiredPermissions) {
        return HasPermissionInternal(context, configuration, requiredPermissions, requireAll: false);
    }

    public static bool HasAllPermissions(
        HttpContext? context,
        IConfiguration configuration,
        params string[] requiredPermissions) {
        return HasPermissionInternal(context, configuration, requiredPermissions, requireAll: true);
    }

    private static bool HasPermissionInternal(
        HttpContext? context,
        IConfiguration configuration,
        string[] requiredPermissions,
        bool requireAll) {
        if (requiredPermissions is not { Length: > 0 }) {
            return true;
        }

        if (context is null) {
            return false;
        }

        if (context.User?.Identity?.IsAuthenticated != true) {
            return false;
        }

        var ownerLogin = configuration["AUTH_OWNER_LOGIN"] ?? "platform.owner@example.local";
        if (ClaimsHelper.IsOwnerLogin(context.User, ownerLogin)) {
            return true;
        }

        var permissionClaims = context.User.Claims
            .Where(c =>
                string.Equals(c.Type, "permission", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Type, "permissions", StringComparison.OrdinalIgnoreCase))
            .SelectMany(c => SplitPermissionClaims(c.Value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return requireAll
            ? requiredPermissions.All(permissionClaims.Contains)
            : requiredPermissions.Any(permissionClaims.Contains);
    }

    private static IEnumerable<string> SplitPermissionClaims(string? raw) {
        if (string.IsNullOrWhiteSpace(raw)) {
            return Array.Empty<string>();
        }

        var value = raw.Trim();
        if (value.StartsWith('[') && value.EndsWith(']')) {
            try {
                var arr = System.Text.Json.JsonSerializer.Deserialize<string[]>(value);
                if (arr is { Length: > 0 }) {
                    return arr
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Select(v => v.Trim());
                }
            }
            catch {
                // Ignore invalid JSON and continue with fallback parsing.
            }
        }

        if (value.Contains(',', StringComparison.Ordinal)) {
            return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        return [value];
    }
}
