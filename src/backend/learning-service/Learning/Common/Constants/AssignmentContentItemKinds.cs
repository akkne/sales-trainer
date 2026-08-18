namespace Sellevate.Learning.Common.Constants;

/// <summary>
/// Phase 40.21. What an assignment may ask somebody to do, as the vocabulary of
/// <c>Assignment.Content</c> (docs/TENANCY/ASSIGNMENTS.md §1, §6).
///
/// <para>
/// <b>Every kind is a reference, and every reference is to something that cannot change underneath a
/// recorded attempt.</b> The design document lists "exercise refs, dialog scenario refs, theory
/// refs"; the first of those is expressed here as <see cref="LessonVersion"/> rather than as a list
/// of <c>Exercise</c> ids, because an <c>Exercise</c> row is the mutable working copy the admin panel
/// edits while a <c>LessonVersion</c> is the frozen ordered set of exercise bodies that 40.15 exists
/// to provide. Pointing an assignment at mutable exercise rows would repeat exactly the mistake 40.16
/// fixed for progress: the person's score would end up describing content nobody can reconstruct.
/// </para>
///
/// <para>
/// The consequence is deliberate and is what makes "no new renderers" (roadmap 40.23) literally true:
/// an assignment's exercises are ordinary exercises inside an ordinary lesson version, played by the
/// screens and graded by the scoring that already exist. Its bodies live in
/// <c>Exercise.SerializedContent</c>, never in this column.
/// </para>
/// </summary>
public static class AssignmentContentItemKinds
{
    /// <summary>
    /// A frozen lesson snapshot — the assignment's exercise set. The reference is a
    /// <c>LessonVersions.Id</c>, never a <c>Lessons.Id</c>.
    /// </summary>
    public const string LessonVersion = "lesson_version";

    /// <summary>
    /// The assignment's practice conversation, named by the ai-service dialog mode key. 40.23 turns
    /// it into an ordinary <c>DialogSession</c> with an injected persona, the same trick
    /// <c>CompanyContextPromptBuilder</c> already uses. A key rather than a uuid because that is how
    /// ai-service addresses modes.
    /// </summary>
    public const string DialogScenario = "dialog_scenario";

    /// <summary>
    /// Theory to read before practising: a <c>ReferenceMaterials.Id</c>. Ungraded, and resolved
    /// through the same override and substitution path as any other read (40.18, 40.19).
    /// </summary>
    public const string ReferenceMaterial = "reference_material";

    public static bool IsKnown(string kind)
        => kind is LessonVersion or DialogScenario or ReferenceMaterial;

    /// <summary>
    /// Whether the kind's reference is a row identifier rather than a free-form key. Used to reject a
    /// content item that names a lesson version or a reference material with something that is not a
    /// uuid — the cheapest place to catch a caller that passed a slug or a title.
    /// </summary>
    public static bool ReferenceIsIdentifier(string kind)
        => kind is LessonVersion or ReferenceMaterial;
}
