using Continuo.Shared.Security;

namespace AuthApi.Infrastructure;

public static class RoleRequirements {
    public static bool HasAnyRole(HttpContext context, params string[] required) {
        if (required == null || required.Length == 0) {
            return true;
        }

        return ClaimsHelper.HasAnyRole(context, required);
    }
}
