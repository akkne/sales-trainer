using MongoDB.Bson.Serialization.Attributes;

namespace Sellevate.Ai.Features.Dialog.Models;

/// <summary>
/// A user-authored role-play brief for the custom-scenario mode: free text describing who the AI
/// should play and what the conversation is about.
/// </summary>
/// <remarks>
/// The text is arbitrary user input and is treated as hostile on two axes. It only reaches a
/// session after <see cref="Services.Abstract.IScenarioValidationService"/> confirms it is about
/// sales, and it only reaches a model prompt through
/// <see cref="Helpers.CustomScenarioPromptBuilder"/>, which fences it as data rather than
/// instructions — the same pattern company/persona context already uses.
/// </remarks>
public sealed class CustomScenarioContext
{
    [BsonElement("scenario")]
    public string Scenario { get; set; } = null!;
}
