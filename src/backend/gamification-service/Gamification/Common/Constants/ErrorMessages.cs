namespace Sellevate.Gamification.Common.Constants;

/// <summary>
/// Every validation and failure message this service returns to a caller. They are part of the API
/// surface — the admin UI shows them verbatim — so the wording lives in one place and cannot drift
/// between two endpoints that reject the same thing.
/// </summary>
public static class ErrorMessages
{
    public const string DeltaMustBeNonZero = "Delta must be non-zero";
    public const string LeagueSettingsMustBePositive = "All settings values must be positive";
    public const string PromotionAndDemotionZonesTooLarge =
        "Promotion + demotion zones cannot exceed maximum participant count";
    public const string PeriodLengthMustBePositive = "Period length must be positive";

    public const string TierKeyRequired = "Key is required";
    public const string TierNameRequired = "Name is required";
    public const string TierColorRequired = "Color is required";
    public const string LastTierCannotBeDeleted = "At least one tier must remain";
    public const string TierInUseCannotBeDeleted =
        "Cannot delete a tier that has existing leagues; reassign members first";

    public const string ExperiencePointsGoalsMustBePositive = "Daily and weekly XP goals must be positive";
    public const string DialogMultiplierMustBePositive = "Dialog XP multiplier must be positive";
    public const string DialogWeightsCannotBeNegative = "Dialog criterion weights cannot be negative";
    public const string DialogWeightSumMustBePositive = "The sum of dialog criterion weights must be positive";
    public const string BaseExperiencePointsRewardCannotBeNegative = "Base XP reward cannot be negative";

    public const string StreakMilestoneDayCountMustBePositive = "Day count must be positive";
    public const string StreakMilestoneRewardCannotBeNegative = "XP reward cannot be negative";

    public const string PostgresConnectionStringMustNameDatabase =
        "ConnectionStrings:Postgres must specify a Database.";

    public static string UnknownTier(string tier) => $"Unknown tier: {tier}";

    public static string TierKeyAlreadyExists(string tierKey) => $"Tier with key '{tierKey}' already exists";

    public static string StreakMilestoneAlreadyExists(int dayCount) =>
        $"A milestone for {dayCount} days already exists";

    /// <summary>
    /// Startup guard message. Spelled from the byte count so the number in the text can never
    /// disagree with the number actually enforced.
    /// </summary>
    public static string JwtSigningKeyTooShort(int minimumByteCount) =>
        $"Jwt:Key must be configured and at least {minimumByteCount} bytes " +
        $"({minimumByteCount * BitsPerByte} bits) long for HMAC-SHA256.";

    private const int BitsPerByte = 8;
}
