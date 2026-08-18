namespace Sellevate.Organization.Common.Constants;

/// <summary>
/// Phase 40.29. The profile's seven fields, named on the wire, so that the promotion of an extracted
/// draft can be argued about field by field instead of accepted or rejected whole.
///
/// <para>
/// <b>Only four of these are ever accept-gated</b> — <see cref="Product"/>, <see cref="Icp"/>,
/// <see cref="Tone"/> and <see cref="ScriptStages"/>, the four that carry a single value a suggestion
/// would have to replace. <see cref="Objections"/>, <see cref="Glossary"/> and
/// <see cref="BannedClaims"/> are merged additively and never replace anything, so there is nothing
/// to consent to; see <c>OrganizationProfileDraftMerger</c> for why the split falls exactly there.
/// </para>
/// </summary>
public static class OrganizationProfileFields
{
    public const string Product = "product";
    public const string Icp = "icp";
    public const string Tone = "tone";
    public const string ScriptStages = "script_stages";
    public const string Objections = "objections";
    public const string Glossary = "glossary";
    public const string BannedClaims = "banned_claims";

    /// <summary>
    /// The fields a caller may name in <c>acceptedFields</c>. Anything else is dropped rather than
    /// rejected, exactly as 40.28 drops an unknown gap code: the vocabulary is closed, and a name the
    /// server does not recognise must not be able to decide what happens to a field.
    /// </summary>
    public static readonly string[] Overwritable =
    [
        Product,
        Icp,
        Tone,
        ScriptStages
    ];

    public static bool IsOverwritable(string? field)
        => field is not null && Overwritable.Contains(field, StringComparer.Ordinal);
}
