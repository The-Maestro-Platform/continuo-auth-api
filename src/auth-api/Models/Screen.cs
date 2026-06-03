using System.Text.Json;

namespace AuthApi.Models;

public class Screen {
    public Ulid Id { get; set; } = Ulid.NewUlid();
    public string AppCode { get; set; } = "tc-ops-ui";
    public string ScreenKey { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public string? RequiredPermissionsJson { get; set; }

    // Navigation support
    public string? Path { get; set; } // URL path for routing
    public string? Icon { get; set; } // Icon identifier
    public string? Group { get; set; } // Menu grouping
    public int SortOrder { get; set; } // Display order in menu

    public bool IsSystem { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public IReadOnlyCollection<string> RequiredPermissions {
        get {
            if (string.IsNullOrWhiteSpace(RequiredPermissionsJson)) {
                return Array.Empty<string>();
            }

            try {
                return JsonSerializer.Deserialize<string[]>(RequiredPermissionsJson) ?? Array.Empty<string>();
            }
            catch {
                return Array.Empty<string>();
            }
        }
    }
}
