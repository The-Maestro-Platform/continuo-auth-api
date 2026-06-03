using AuthApi.Services.Wizard;
using MassTransit;
using Continuo.Persistence.Idempotency;
using Continuo.Shared.Contracts;

namespace AuthApi.Consumers;

/// <summary>
/// V2 Wizard Phase F4 (auth-api side) — direct consumer for
/// <see cref="DefaultRoleSeedRequestedEvent"/>. Persists the package-aware
/// role + permission map into auth-api's <c>Roles</c> / <c>RolePermissions</c>
/// tables so the tenant owner can log in with the right access without
/// platform ops running the seeder by hand.
///
/// The security-api sibling consumer of the same event still emits a
/// <see cref="Continuo.Messaging.Notifications.PlatformAdminAlertRequestedEvent"/>;
/// keeping both is intentional defense-in-depth — if a permission key in
/// the payload is missing from the global catalog, auth-api's seeder
/// reports it via <see cref="DefaultRoleSeederResult.MissingPermissionKeys"/>
/// and the security-api alert tells ops to seed those permissions before
/// re-running the wizard.
///
/// Two-layer idempotency:
/// <list type="bullet">
/// <item>Inbox per <see cref="DefaultRoleSeedRequestedEvent.RequestId"/> blocks
///     duplicate MT deliveries.</item>
/// <item>Seeder upserts on (tenantSlug.roleCode) — replay-safe even if the
///     inbox row was lost.</item>
/// </list>
/// </summary>
public sealed class DefaultRoleSeedRequestedConsumer
    : IConsumer<DefaultRoleSeedRequestedEvent> {
    private const string ConsumerKey = "auth-api.default-role-seed";

    private readonly IDefaultRoleSeederService _seeder;
    private readonly IInboxStore _inbox;
    private readonly ILogger<DefaultRoleSeedRequestedConsumer> _log;

    public DefaultRoleSeedRequestedConsumer(
        IDefaultRoleSeederService seeder,
        IInboxStore inbox,
        ILogger<DefaultRoleSeedRequestedConsumer> log) {
        _seeder = seeder;
        _inbox = inbox;
        _log = log;
    }

    public async Task Consume(ConsumeContext<DefaultRoleSeedRequestedEvent> context) {
        var msg = context.Message;
        var idempotencyKey = msg.RequestId.ToString("N");
        if (await _inbox.WasProcessedAsync(ConsumerKey, idempotencyKey, context.CancellationToken)) {
            _log.LogInformation(
                "DefaultRoleSeed already processed for tenant {Slug} request {Req}",
                msg.TenantSlug, msg.RequestId);
            return;
        }

        var result = await _seeder.SeedAsync(msg, context.CancellationToken);
        await _inbox.RecordAsync(ConsumerKey, idempotencyKey, context.CancellationToken);

        _log.LogInformation(
            "DefaultRoleSeed processed for tenant {Slug} request {Req}: inserted={Ins} updated={Upd} skipped={Skp} missingPerms={Missing}",
            msg.TenantSlug, msg.RequestId,
            result.InsertedRoles, result.UpdatedRoles, result.SkippedRoles,
            string.Join(",", result.MissingPermissionKeys));
    }
}
