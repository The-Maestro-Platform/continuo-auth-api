using System.ComponentModel.DataAnnotations;

namespace AuthApi.Models;

public class TenantUser {
    [Key]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [MaxLength(26)]
    public Ulid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    [MaxLength(160)]
    public string DisplayName { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? FirstName { get; set; }

    [MaxLength(120)]
    public string? LastName { get; set; }

    [MaxLength(160)]
    public string? Email { get; set; }

    [MaxLength(32)]
    public string? PhoneNumber { get; set; }

    [MaxLength(200)]
    public string? AddressLine1 { get; set; }

    [MaxLength(200)]
    public string? AddressLine2 { get; set; }

    [MaxLength(120)]
    public string? City { get; set; }

    [MaxLength(120)]
    public string? Country { get; set; }

    [MaxLength(32)]
    public string? PostalCode { get; set; }

    [MaxLength(160)]
    public string? PositionTitle { get; set; }

    public bool MarketingOptIn { get; set; }

    public TenantUserStatus Status { get; set; } = TenantUserStatus.Active;

    public bool IsActive => Status == TenantUserStatus.Active;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<UserRole> Roles { get; set; } = new List<UserRole>();
    public ICollection<Credential> Credentials { get; set; } = new List<Credential>();
}
