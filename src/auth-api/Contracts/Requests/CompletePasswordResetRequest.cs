namespace AuthApi.Contracts.Requests;

public record CompletePasswordResetRequest(
    string Token,
    string NewPassword);
