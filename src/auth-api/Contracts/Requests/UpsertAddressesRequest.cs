using AuthApi.Models;
using AuthApi.Services;

namespace AuthApi.Contracts.Requests;

public record UpsertAddressesRequest(CommunicationOwnerType OwnerType, string OwnerId, AddressDto[]? Addresses);
