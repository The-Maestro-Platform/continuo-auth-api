namespace AuthApi.Seed;

/// <summary>
/// Canonical platform-agreement code constants. Phase-1 move-only extraction
/// from <see cref="DefaultPlatformAgreements"/>; the original public consts
/// forward here so the existing public API is preserved.
/// </summary>
public static class PlatformAgreementCodes {
    public const string CodeTerms = "terms";
    public const string CodeKvkk = "kvkk";
    public const string CodeMarketing = "marketing";
    public const string CodePlatformSubscription = "platform_subscription";
}
