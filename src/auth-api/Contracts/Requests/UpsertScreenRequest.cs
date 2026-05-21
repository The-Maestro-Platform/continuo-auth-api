namespace AuthApi.Contracts.Requests;

public record UpsertScreenRequest(string AppCode, string ScreenKey, string Title, string? Description, string[]? RequiredPermissions, bool IsSystem = false);
