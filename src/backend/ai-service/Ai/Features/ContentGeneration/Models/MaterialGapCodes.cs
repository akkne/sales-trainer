namespace Sellevate.Ai.Features.ContentGeneration.Models;

/// <summary>
/// Phase 40.28. The closed vocabulary the model may use to say what is missing from the material.
///
/// <para>
/// <b>Codes, not sentences.</b> The refusal the РОП reads («добавьте примеры возражений или запись
/// звонка») is written by learning-service, once, in one place, and is the same sentence every time.
/// If the model wrote the refusal itself it would be a different sentence on every run, would be
/// untranslatable, and would occasionally invent a demand the product cannot satisfy — «пришлите
/// договор», «загрузите видео». A closed list also means a code the model invents is simply dropped
/// rather than shown to a customer.
/// </para>
///
/// <para>
/// The same list exists on the other side of the wire as
/// <c>Sellevate.Learning.Common.Constants.ContentSufficiencyCodes</c>, which additionally holds the
/// Russian sentences. It is redeclared rather than shared for the reason
/// <see cref="ExtractedObjectionDto"/> is: this is the wire shape of an internal endpoint and it must
/// not move when a learning-service constant does.
/// </para>
/// </summary>
public static class MaterialGapCodes
{
    /// <summary>The material is not about selling at all — a recipe, a manual, a policy document.</summary>
    public const string OffTopic = "off_topic";

    /// <summary>There is material, and there is not enough of it to build a lesson from.</summary>
    public const string TooShort = "too_short";

    /// <summary>Nothing says what the company sells.</summary>
    public const string NoProduct = "no_product";

    /// <summary>Nothing says who they sell it to.</summary>
    public const string NoIcp = "no_icp";

    /// <summary>No objection a client actually voices appears anywhere in it.</summary>
    public const string NoObjections = "no_objections";

    /// <summary>No call script, no stages, nothing about how a conversation is meant to go.</summary>
    public const string NoScript = "no_script";

    /// <summary>Abstractions only — no live wording, no quoted lines, nothing to build an exercise on.</summary>
    public const string NoExamples = "no_examples";

    public static readonly string[] All =
    [
        OffTopic,
        TooShort,
        NoProduct,
        NoIcp,
        NoObjections,
        NoScript,
        NoExamples
    ];

    public static bool IsKnown(string? code) => code is not null && All.Contains(code);
}
