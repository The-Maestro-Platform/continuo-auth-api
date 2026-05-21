using AuthApi.Models;

namespace AuthApi.Contracts.Requests;

public sealed class CreateRoleRequest {
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public RoleScope? Scope { get; init; }
    public string[]? PermissionKeys { get; init; }
    // Backward compatibility for older UIs sending "permissionCodes"
    public string[]? PermissionCodes { get; init; }
}
