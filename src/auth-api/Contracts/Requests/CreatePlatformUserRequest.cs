namespace AuthApi.Contracts.Requests;

public record CreatePlatformUserRequest(string Email, string DisplayName, string Password, string[]? RoleIds);
