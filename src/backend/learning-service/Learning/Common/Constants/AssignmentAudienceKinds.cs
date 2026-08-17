namespace Sellevate.Learning.Common.Constants;

/// <summary>
/// Phase 40.21. Who an assignment is for, as the vocabulary of <c>Assignment.Audience</c>
/// (roadmap 40.23: "конкретные пользователи / группа / вся команда").
///
/// <para>
/// <b>The audience column stores the rule, not the people.</b> The list of an organization's
/// employees lives in identity-service (<c>Memberships</c>); learning-db holds only
/// <c>UserReplicas</c>, which is platform-global and says nothing about who belongs where. So
/// learning-service cannot resolve "the whole team" into names without asking identity, and a column
/// that tried to hold the resolved list would be a stale copy of somebody else's data the moment
/// anybody is hired or leaves.
/// </para>
///
/// <para>
/// The resolution is 40.23's job, and its output is the set of <c>AssignmentProgressRecords</c> rows
/// — which is also the authoritative answer to "who actually got this", because it is the only
/// record written at issue time rather than re-derived later.
/// </para>
/// </summary>
public static class AssignmentAudienceKinds
{
    /// <summary>Everyone in the organization at issue time. Carries no further fields.</summary>
    public const string WholeTeam = "whole_team";

    /// <summary>
    /// A named list of user ids. Stored as given: learning-service deliberately does not check them
    /// against membership, because it cannot — see the class remarks.
    /// </summary>
    public const string Users = "users";

    /// <summary>
    /// One group of people. Declared now so 40.23 need not migrate the column, but nothing in the
    /// platform defines a group yet: the shape is validated, the meaning is not.
    /// </summary>
    public const string Group = "group";

    public static bool IsKnown(string kind)
        => kind is WholeTeam or Users or Group;
}
