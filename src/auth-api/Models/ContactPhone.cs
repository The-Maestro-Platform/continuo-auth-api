using System.ComponentModel.DataAnnotations;

namespace AuthApi.Models;

public class ContactPhone {
    [Key]
    public Ulid Id { get; set; } = Ulid.NewUlid();

    [MaxLength(26)]
    public Ulid CommunicationInfoId { get; set; }
    public CommunicationInfo CommunicationInfo { get; set; } = null!;

    public ContactPhoneType Type { get; set; } = ContactPhoneType.Mobile;

    [MaxLength(8)]
    public string? CountryCode { get; set; }

    [MaxLength(32)]
    public string Number { get; set; } = string.Empty;

    [MaxLength(16)]
    public string? Extension { get; set; }

    [MaxLength(200)]
    public string? Notes { get; set; }

    public bool IsPrimary { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
