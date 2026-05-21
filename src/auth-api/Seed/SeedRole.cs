using AuthApi.Models;

namespace AuthApi.Seed;

public record SeedRole(string Name, string Description, RoleScope Scope, string[] PermissionKeys, bool IsSystem = true);
