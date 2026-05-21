using System.Text.Json;
using System.Text.Json.Serialization;
using AuthApi.Models;
using Continuo.Persistence.Json;
using Continuo.Persistence.Outbox;
using Continuo.Shared.Contracts;

namespace AuthApi.Services;

internal static class CustomerOutboxFactory {
    private static readonly JsonSerializerOptions ContractJsonOptions = new() {
        Converters = { new JsonStringEnumConverter(), new UlidJsonConverter() }
    };

    public static OutboxMessage Registered(Customer customer, string tenantCode) {
        var occurredAt = DateTime.UtcNow;
        var evt = new CustomerRegisteredEvent(
            EventId: Guid.NewGuid(),
            CustomerId: customer.Id,
            TenantId: customer.TenantId,
            TenantCode: tenantCode,
            Email: customer.Email,
            Phone: customer.PhoneNumber,
            DisplayName: customer.DisplayName,
            FullName: customer.FullName,
            AddressLine1: customer.AddressLine1,
            AddressLine2: customer.AddressLine2,
            City: customer.City,
            Country: customer.Country,
            PostalCode: customer.PostalCode,
            MarketingOptIn: customer.MarketingOptIn,
            OccurredAt: occurredAt,
            Version: customer.Version);

        return Create(CustomerEventTypes.Registered, evt, customer, tenantCode, occurredAt);
    }

    public static OutboxMessage ProfileUpdated(Customer customer, string tenantCode) {
        var occurredAt = DateTime.UtcNow;
        var evt = new CustomerProfileUpdatedEvent(
            EventId: Guid.NewGuid(),
            CustomerId: customer.Id,
            TenantId: customer.TenantId,
            TenantCode: tenantCode,
            Email: customer.Email,
            Phone: customer.PhoneNumber,
            DisplayName: customer.DisplayName,
            FullName: customer.FullName,
            AddressLine1: customer.AddressLine1,
            AddressLine2: customer.AddressLine2,
            City: customer.City,
            Country: customer.Country,
            PostalCode: customer.PostalCode,
            MarketingOptIn: customer.MarketingOptIn,
            OccurredAt: occurredAt,
            Version: customer.Version);

        return Create(CustomerEventTypes.ProfileUpdated, evt, customer, tenantCode, occurredAt);
    }

    public static OutboxMessage OptInChanged(Customer customer, string tenantCode) {
        var occurredAt = DateTime.UtcNow;
        var evt = new CustomerOptInChangedEvent(
            EventId: Guid.NewGuid(),
            CustomerId: customer.Id,
            TenantId: customer.TenantId,
            TenantCode: tenantCode,
            MarketingOptIn: customer.MarketingOptIn,
            OccurredAt: occurredAt,
            Version: customer.Version);

        return Create(CustomerEventTypes.OptInChanged, evt, customer, tenantCode, occurredAt);
    }

    public static OutboxMessage AgreementsAccepted(
        Customer customer,
        string tenantCode,
        string agreementsVersion,
        DateTime acceptedAt) {
        var occurredAt = DateTime.UtcNow;
        var evt = new CustomerAgreementsAcceptedEvent(
            EventId: Guid.NewGuid(),
            CustomerId: customer.Id,
            TenantId: customer.TenantId,
            TenantCode: tenantCode,
            AgreementsVersion: agreementsVersion,
            AcceptedAt: acceptedAt,
            MarketingOptIn: customer.MarketingOptIn,
            OccurredAt: occurredAt,
            Version: customer.Version);

        return Create(CustomerEventTypes.AgreementsAccepted, evt, customer, tenantCode, occurredAt);
    }

    private static OutboxMessage Create<T>(
        string type,
        T evt,
        Customer customer,
        string tenantCode,
        DateTime occurredAt) where T : class {
        return new OutboxMessage {
            Id = Ulid.NewUlid(),
            OccurredOn = occurredAt,
            Type = type,
            Payload = JsonSerializer.Serialize(evt, ContractJsonOptions),
            Processed = false,
            TenantCode = tenantCode,
            TenantId = customer.TenantId.ToString()
        };
    }
}
