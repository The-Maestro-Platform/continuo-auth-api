namespace AuthApi.Contracts.Requests;

public record ForgotPasswordRequest(
    string? Login,
    string? Email,
    string? ResetPath,
    string? ResetOrigin);
