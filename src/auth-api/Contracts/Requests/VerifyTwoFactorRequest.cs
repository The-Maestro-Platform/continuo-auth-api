namespace AuthApi.Contracts.Requests;

public record VerifyTwoFactorRequest(string ChallengeId, string Code);
