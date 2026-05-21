using AuthApi.Models;
using AuthApi.Services;

namespace AuthApi.Contracts.Requests;

public record UpsertPhonesRequest(CommunicationOwnerType OwnerType, string OwnerId, PhoneDto[]? Phones);
