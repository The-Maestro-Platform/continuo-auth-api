namespace AuthApi.Models;

public class UserSession {
    public Ulid Id { get; set; } = Ulid.NewUlid();
    public Ulid CredentialId { get; set; }
    public Credential Credential { get; set; } = null!;

    public string AppId { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }
    public string? RevokedReason { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    // SHA-256(base64url) hash of the opaque session token handed to the browser
    // cookie. The plaintext token never persists. Lookups go by hash, so a DB
    // dump cannot resurrect anyone's session. Nullable for the rolling deploy
    // window where pre-opaque sessions still exist; fresh logins always fill it.
    public string? SessionTokenHash { get; set; }
}

public static class UserSessionRevocationReasons {
    public const string Displaced = "displaced";
    public const string Logout = "logout";
    public const string Idle = "idle";
    public const string PasswordReset = "password_reset";
}
