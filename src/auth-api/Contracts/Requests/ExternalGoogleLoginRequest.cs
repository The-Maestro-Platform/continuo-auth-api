namespace AuthApi.Contracts.Requests;

public sealed record ExternalGoogleLoginRequest(
    string IdToken,
    string? TenantCode = null,
    bool AutoRegister = false
);
