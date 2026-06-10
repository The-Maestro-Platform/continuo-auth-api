using AuthApi.Models;

namespace AuthApi.Controllers;

/// <summary>
/// Stateless read-only transformations extracted from <see cref="AuthController"/>
/// during Phase-1 move-only refactor. No closure over controller state.
/// </summary>
internal static class AuthHelpers {
    public static bool LooksLikeJwt(string value) {
        var parts = value.Split('.');
        return parts.Length == 3 && !string.IsNullOrEmpty(parts[0]) && !string.IsNullOrEmpty(parts[1]) && !string.IsNullOrEmpty(parts[2]);
    }

    public static string ResolveDisplayName(Credential credential) {
        return credential.OwnerType switch {
            CredentialOwnerType.PlatformUser => credential.PlatformUser?.DisplayName ?? credential.Login,
            CredentialOwnerType.TenantUser => credential.TenantUser?.DisplayName ?? credential.Login,
            CredentialOwnerType.Customer => credential.Customer?.DisplayName ?? credential.Login,
            _ => credential.Login
        };
    }

    public static string MaskTarget(string target) {
        if (string.IsNullOrWhiteSpace(target)) {
            return string.Empty;
        }

        var atIdx = target.IndexOf('@');
        if (atIdx > 0) {
            var local = target.Substring(0, atIdx);
            var domain = target.Substring(atIdx);
            if (local.Length <= 2) {
                return new string('*', local.Length) + domain;
            }

            return $"{local[..2]}***{domain}";
        }

        if (target.Length <= 4) {
            return "****";
        }

        return $"{target[..2]}***{target[^1]}";
    }

    public static IEnumerable<Role> ResolveRoles(Credential credential) {
        return credential.OwnerType switch {
            CredentialOwnerType.PlatformUser => credential.PlatformUser?.Roles.Select(r => r.Role) ?? Enumerable.Empty<Role>(),
            CredentialOwnerType.TenantUser => credential.TenantUser?.Roles.Select(r => r.Role) ?? Enumerable.Empty<Role>(),
            _ => Enumerable.Empty<Role>()
        };
    }

    public static IEnumerable<UserRole> ResolveUserRoles(Credential credential) {
        return credential.OwnerType switch {
            CredentialOwnerType.PlatformUser => credential.PlatformUser?.Roles ?? Enumerable.Empty<UserRole>(),
            CredentialOwnerType.TenantUser => credential.TenantUser?.Roles ?? Enumerable.Empty<UserRole>(),
            _ => Enumerable.Empty<UserRole>()
        };
    }
}
