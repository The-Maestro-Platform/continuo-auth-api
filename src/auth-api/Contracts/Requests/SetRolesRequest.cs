namespace AuthApi.Contracts.Requests;

public record SetRolesRequest(string[]? RoleIds, BranchRoleAssignment[]? BranchRoles);
