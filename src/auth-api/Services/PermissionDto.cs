using AuthApi.Models;

namespace AuthApi.Services;

public record PermissionDto(string Key, string DisplayName, string? Description, RoleScope Scope);
