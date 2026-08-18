namespace Sellevate.Ai.Features.Dialog.Constants;

/// <summary>Bounds on a user-authored custom scenario, shared by the controller and the validator.</summary>
public static class ScenarioLimits
{
    /// <summary>Shortest scenario we accept — below this there is nothing to role-play.</summary>
    public const int MinimumLength = 20;

    /// <summary>Longest scenario we accept, to bound both prompt size and moderation cost.</summary>
    public const int MaximumLength = 1500;
}
