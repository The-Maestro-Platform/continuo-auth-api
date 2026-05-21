using System.ComponentModel.DataAnnotations;

namespace AuthApi.Models;

public class ContactAddress {
    [Key]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [MaxLength(26)]
    public Ulid CommunicationInfoId { get; set; }
    public CommunicationInfo CommunicationInfo { get; set; } = null!;

    [MaxLength(80)]
    public string? Label { get; set; }

    public ContactAddressType Type { get; set; } = ContactAddressType.Other;

    [MaxLength(200)]
    public string Line1 { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Line2 { get; set; }

    [MaxLength(120)]
    public string? City { get; set; }

    [MaxLength(120)]
    public string? Country { get; set; }

    [MaxLength(32)]
    public string? PostalCode { get; set; }

    [MaxLength(200)]
    public string? Notes { get; set; }

    public bool IsPrimary { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
