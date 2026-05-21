namespace AuthApi.Contracts.Requests;

public record LoginRequest(string? Login, string? Email, string Password);
