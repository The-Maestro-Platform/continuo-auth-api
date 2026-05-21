namespace AuthApi.Services;

public sealed class OutboxTwoFactorNotifier : ITwoFactorNotifier {
    private readonly AuthDbContext _db;
    private readonly ILogger<OutboxTwoFactorNotifier> _logger;
    private const string TwoFactorChallengeCreatedType = "TwoFactor.Challenge.Created";

    public OutboxTwoFactorNotifier(AuthDbContext db, ILogger<OutboxTwoFactorNotifier> logger) {
        _db = db;
        _logger = logger;
    }

    public async Task NotifyAsync(TwoFactorDispatchPayload payload, CancellationToken ct) {
        _db.OutboxMessages.Add(new Continuo.Persistence.Outbox.OutboxMessage {
            Type = TwoFactorChallengeCreatedType,
            Payload = System.Text.Json.JsonSerializer.Serialize(new Continuo.Shared.Contracts.TwoFactorChallengeCreatedEvent(
                payload.ChallengeId,
                payload.Channel,
                payload.Target,
                payload.Code,
                payload.ExpiresAtUtc,
                payload.DisplayName,
                payload.TenantName,
                payload.Flow)),
            OccurredOn = DateTime.UtcNow,
            Processed = false
        });

        // Bug fix: önceki implementasyon Add() yapıp SaveChanges'i caller'a bırakıyordu.
        // TwoFactorService.RequestChallengeAsync zaten Challenge entity'sini save ettikten
        // SONRA NotifyAsync çağırıyor → Notifier'ın eklediği OutboxMessage row asla
        // persist edilmiyordu (scope dispose'la birlikte tracking kayboluyordu).
        // Sonuç: aut.OutboxMessages boş → OutboxDispatcher publish edemiyor →
        // notification-api 2FA event'i hiç almıyor → kullanıcı kodu hiç görmüyor.
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Queued 2FA notification to outbox for challenge {ChallengeId}", payload.ChallengeId);
    }
}
