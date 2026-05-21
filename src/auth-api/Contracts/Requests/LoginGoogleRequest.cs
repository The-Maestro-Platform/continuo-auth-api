namespace AuthApi.Contracts.Requests;

public sealed record LoginGoogleRequest(string IdToken, bool AutoRegister = false);

