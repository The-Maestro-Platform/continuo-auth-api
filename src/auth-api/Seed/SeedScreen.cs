namespace AuthApi.Seed;

public record SeedScreen(
    string AppCode,
    string ScreenKey,
    string Title,
    string? Description,
    string[] RequiredPermissions,
    string? Path = null,
    string? Icon = null,
    string? Group = null,
    int SortOrder = 0,
    bool IsSystem = true);
