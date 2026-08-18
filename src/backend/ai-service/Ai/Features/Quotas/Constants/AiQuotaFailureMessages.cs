namespace Sellevate.Ai.Features.Quotas.Constants;

/// <summary>
/// The <c>error</c> text of the two refusal bodies this feature returns. Both are part of the wire
/// contract in <c>docs/API_CONTRACTS.md</c> and sibling services assert on them, so they are stated
/// once here rather than typed at the throw site.
/// </summary>
public static class AiQuotaFailureMessages
{
    /// <summary>Body of the 429 an organization gets when its allowance is spent.</summary>
    public const string QuotaReached = "Organization AI quota reached";

    /// <summary>Body of the 400 an internal caller gets when it forwarded no organization.</summary>
    public const string OrganizationRequired = "An organization is required for metered AI calls.";
}
