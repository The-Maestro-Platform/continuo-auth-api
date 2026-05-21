using AuthApi.Models;
using Microsoft.EntityFrameworkCore;

namespace AuthApi.Services;

public class NavigationService {
    private readonly AuthDbContext _context;

    public NavigationService(AuthDbContext context) {
        _context = context;
    }

    public async Task<List<NavigationItem>> GetNavigationAsync(
        string appCode,
        IReadOnlyList<string> screenIds) {
        var screenIdSet = screenIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (screenIdSet.Contains("*")) {
            var allScreens = await _context.Screens
                .Where(s => s.AppCode == appCode && !string.IsNullOrEmpty(s.Path))
                .OrderBy(s => s.SortOrder)
                .ThenBy(s => s.Title)
                .ToListAsync();
            return MapScreensToNavigation(allScreens);
        }

        var screenIdsParsed = screenIds
            .Where(id => Ulid.TryParse(id, out _))
            .Select(Ulid.Parse)
            .Distinct()
            .ToArray();
        var screenKeys = screenIds
            .Where(id => !string.IsNullOrWhiteSpace(id) && !id.StartsWith("/") && !Ulid.TryParse(id, out _))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var screenPaths = screenIds
            .Where(id => !string.IsNullOrWhiteSpace(id) && id.StartsWith("/"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var accessibleScreens = await _context.Screens
            .Where(s =>
                s.AppCode == appCode &&
                !string.IsNullOrEmpty(s.Path) &&
                (screenIdsParsed.Contains(s.Id) ||
                 screenKeys.Contains(s.ScreenKey) ||
                 (s.Path != null && screenPaths.Contains(s.Path))))
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Title)
            .ToListAsync();
        return MapScreensToNavigation(accessibleScreens);
    }

    private List<NavigationItem> MapScreensToNavigation(List<Screen> screens) {
        var items = new List<NavigationItem>();
        foreach (var screen in screens) {
            items.Add(new NavigationItem {
                Id = screen.ScreenKey,
                Path = screen.Path!,
                Title = screen.Title,
                Description = screen.Description,
                Icon = screen.Icon,
                Group = screen.Group,
                SortOrder = screen.SortOrder,
                RequiredPermissions = screen.RequiredPermissions.ToList()
            });
        }
        return items;
    }

    public async Task<Dictionary<string, List<NavigationItem>>> GetGroupedNavigationAsync(
        string appCode,
        IReadOnlyList<string> screenIds) {
        var items = await GetNavigationAsync(appCode, screenIds);
        return items.GroupBy(i => i.Group ?? "General").ToDictionary(g => g.Key, g => g.OrderBy(i => i.SortOrder).ToList());
    }
}

public class NavigationItem {
    public string Id { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Icon { get; set; }
    public string? Group { get; set; }
    public int SortOrder { get; set; }
    public List<string> RequiredPermissions { get; set; } = new();
}
