using System.ComponentModel.DataAnnotations;

namespace AuthApi.Models;

public class Tenant {
    [Key]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [MaxLength(40)]
    public string Code { get; set; } = string.Empty;

    [MaxLength(160)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? Slug { get; set; }

    [MaxLength(80)]
    public string? Subdomain { get; set; }

    [MaxLength(160)]
    public string? ContactEmail { get; set; }

    [MaxLength(32)]
    public string? ContactPhone { get; set; }

    public TenantStatus Status { get; set; } = TenantStatus.Active;

    [MaxLength(400)]
    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<TenantUser> Users { get; set; } = new List<TenantUser>();
    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
}
