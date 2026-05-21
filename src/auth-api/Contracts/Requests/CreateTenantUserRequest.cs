namespace AuthApi.Contracts.Requests;

public record CreateTenantUserRequest(
    string TenantCode,
    string Email,
    string? DisplayName,
    string Password,
    string[]? RoleIds,
    BranchRoleAssignment[]? BranchRoles);
