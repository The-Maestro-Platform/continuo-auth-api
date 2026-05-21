namespace AuthApi.Services;

public interface ITwoFactorNotifier {
    Task NotifyAsync(TwoFactorDispatchPayload payload, CancellationToken ct);
}
