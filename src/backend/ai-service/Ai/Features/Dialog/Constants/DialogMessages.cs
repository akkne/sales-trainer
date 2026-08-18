namespace Sellevate.Ai.Features.Dialog.Constants;

/// <summary>
/// Text the dialog feature answers a rejected request with. Two audiences are mixed here on purpose
/// and the language marks which is which: Russian strings reach a learner in the product, English
/// strings are API-contract notes for whoever wrote the client.
/// </summary>
public static class DialogMessages
{
    /// <summary>
    /// Reachable by hand-editing the URL of a custom-scenario conversation, so it is user-facing
    /// Russian rather than an API-contract note.
    /// </summary>
    public const string ScenarioDescriptionRequired = "Нужно описать сценарий, чтобы начать разговор.";

    /// <summary>Used when the moderator refused a scenario but supplied no reason of its own.</summary>
    public const string ScenarioNotAboutSales = "Недопустимый сценарий: он не связан с продажами.";

    /// <summary>Shown when the relevance check could not reach the model at all.</summary>
    public const string ScenarioCheckUnavailable = "Не удалось проверить сценарий. Попробуйте ещё раз через минуту.";

    public const string BundleNotFound = "Bundle not found";
    public const string ModeNotFound = "Mode not found";
    public const string SessionNotFound = "Session not found";
    public const string CompanyCallModeNotSeeded = "Company-call mode is not seeded yet.";
    public const string CustomScenarioModeNotSeeded = "Custom-scenario mode is not seeded yet.";
}
