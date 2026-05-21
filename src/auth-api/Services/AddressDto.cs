using AuthApi.Models;

namespace AuthApi.Services;

public class AddressDto {
    public string? Id { get; set; }
    public string? Label { get; set; }
    public ContactAddressType Type { get; set; }
    public required string Line1 { get; set; }
    public string? Line2 { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? PostalCode { get; set; }
    public string? Notes { get; set; }
    public bool IsPrimary { get; set; }
}
