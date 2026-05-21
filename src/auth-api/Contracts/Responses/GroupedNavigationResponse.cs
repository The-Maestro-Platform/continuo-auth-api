using AuthApi.Services;

namespace AuthApi.Contracts.Responses;

public class GroupedNavigationResponse {
    public Dictionary<string, List<NavigationItem>> Groups { get; set; } = new();
}
