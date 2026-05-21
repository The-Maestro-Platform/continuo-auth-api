namespace AuthApi.Contracts.Requests;

public record BranchRoleAssignment(string RoleId, string? BranchCode);

public record CreateCredentialRequest(
    string Login,
    string Password,
    string DisplayName,
    string TenantCode,
    string? FirstName,
    string? LastName,
    string? Email,
    string? PhoneNumber,
    string? City,
    string? Country,
    string? PositionTitle,
    bool MarketingOptIn,
    string[]? RoleIds,
    BranchRoleAssignment[]? BranchRoles);
