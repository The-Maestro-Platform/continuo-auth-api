using AuthApi.Models;

namespace AuthApi.Services;

public class CommunicationDto {
    public required string OwnerId { get; set; }
    public CommunicationOwnerType OwnerType { get; set; }
    public List<AddressDto> Addresses { get; set; } = new();
    public List<PhoneDto> Phones { get; set; } = new();
}
