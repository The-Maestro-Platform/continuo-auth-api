using AuthApi.Services;

namespace AuthApi.Contracts.Responses;

public class NavigationResponse {
    public List<NavigationItem> Items { get; set; } = new();
}
