using System.ComponentModel.DataAnnotations;

namespace AuthApi.Models;

public class CommunicationInfo {
    [Key]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    public CommunicationOwnerType OwnerType { get; set; }

    [MaxLength(26)]
    public Ulid? PlatformUserId { get; set; }
    public PlatformUser? PlatformUser { get; set; }

    [MaxLength(26)]
    public Ulid? TenantUserId { get; set; }
    public TenantUser? TenantUser { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<ContactAddress> Addresses { get; set; } = new List<ContactAddress>();
    public ICollection<ContactPhone> Phones { get; set; } = new List<ContactPhone>();
}
