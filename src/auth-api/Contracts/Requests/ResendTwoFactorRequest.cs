namespace AuthApi.Contracts.Requests;

public record ResendTwoFactorRequest(string ChallengeId);
