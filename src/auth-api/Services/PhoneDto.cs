using AuthApi.Models;

namespace AuthApi.Services;

public class PhoneDto {
    public string? Id { get; set; }
    public ContactPhoneType Type { get; set; }
    public string? CountryCode { get; set; }
    public required string Number { get; set; }
    public string? Extension { get; set; }
    public string? Notes { get; set; }
    public bool IsPrimary { get; set; }
}
