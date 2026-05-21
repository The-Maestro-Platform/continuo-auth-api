using System.ComponentModel.DataAnnotations;

namespace AuthApi.Models;

public class Customer {
    [Key]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [MaxLength(26)]
    public Ulid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    [MaxLength(160)]
    public string? Email { get; set; }

    [MaxLength(32)]
    public string? PhoneNumber { get; set; }

    [MaxLength(200)]
    public string? DisplayName { get; set; }

    [MaxLength(120)]
    public string? LoyaltyWalletId { get; set; }

    [MaxLength(160)]
    public string? FullName { get; set; }

    [MaxLength(120)]
    public string? City { get; set; }

    [MaxLength(120)]
    public string? Country { get; set; }

    [MaxLength(200)]
    public string? AddressLine1 { get; set; }

    [MaxLength(200)]
    public string? AddressLine2 { get; set; }

    [MaxLength(32)]
    public string? PostalCode { get; set; }

    public bool MarketingOptIn { get; set; }

    public long Version { get; set; } = 1;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Credential> Credentials { get; set; } = new List<Credential>();
}
