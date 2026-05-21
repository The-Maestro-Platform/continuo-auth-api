using AuthApi.Models;

namespace AuthApi.Services;

public class TwoFactorOptions {
    public bool Enabled { get; set; } = true;
    public int CodeLength { get; set; } = 6;
    public int CodeTtlMinutes { get; set; } = 10;
    public int MaxAttempts { get; set; } = 5;
    public string PreferredChannel { get; set; } = "Email";
    public string[] RequiredOwnerTypes { get; set; } = new[] { CredentialOwnerType.PlatformUser.ToString(), CredentialOwnerType.TenantUser.ToString() };
    public string FlowName { get; set; } = "Continuo Portal";

    /// <summary>
    /// Bootstrap login whitelist — these credentials always bypass 2FA, even
    /// when <see cref="Enabled"/>=true and <see cref="RequiredOwnerTypes"/>
    /// would otherwise force it. Use cases:
    ///   - Initial platform.owner login on a fresh deploy where the email/SMS
    ///     dispatch channel isn't configured yet (chicken-and-egg).
    ///   - Break-glass admin access if the channel provider is down.
    /// Match is case-insensitive on Credential.Login.
    /// Configure via env, e.g.:
    ///   TwoFactor__BypassEmails__0=platform.owner@example.local
    /// </summary>
    public string[] BypassEmails { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Minimum seconds between two resend-code requests on the same challenge.
    /// Prevents abuse of the email dispatch outbox by rapid resend clicks.
    /// </summary>
    public int ResendCooldownSeconds { get; set; } = 30;

    /// <summary>
    /// Lifetime (in days) of a trusted-device token. Once a credential has
    /// completed 2FA from a browser, that browser stays trusted for this many
    /// days and skips the 2FA prompt on subsequent logins.
    /// </summary>
    public int TrustedDeviceTtlDays { get; set; } = 30;
}
