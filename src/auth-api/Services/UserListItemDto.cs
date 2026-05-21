using AuthApi.Models;

namespace AuthApi.Services;

public class UserListItemDto {
    public required string Id { get; set; }
    public string? DisplayName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? PositionTitle { get; set; }
    public bool MarketingOptIn { get; set; }
    public TenantUserStatus Status { get; set; }
    public required TenantSlim Tenant { get; set; }
    public required List<RoleSlim> Roles { get; set; }
    public required List<string> CredentialIds { get; set; }
}
