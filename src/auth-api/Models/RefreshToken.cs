namespace AuthApi.Models;

public class RefreshToken {
    public Ulid Id { get; set; } = Ulid.NewUlid();
    public string Token { get; set; } = null!;
    public DateTime Expires { get; set; }
    public DateTime Created { get; set; }
    public bool Revoked { get; set; }
    public Ulid CredentialId { get; set; }
    public Credential Credential { get; set; } = null!;
    public DateTime? ProcessedOn { get; set; }
}
