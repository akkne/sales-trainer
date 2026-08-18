using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Assignments.Models;
using Sellevate.Learning.Features.Assignments.Services.Abstract;
using Sellevate.Learning.Infrastructure.Identity;

namespace Sellevate.Learning.Features.Assignments.Services.Implementation;

/// <summary>
/// Phase 40.23. The step 40.21 named and deliberately did not build: an audience rule becomes the
/// people it means (docs/TENANCY/ASSIGNMENTS.md §1).
///
/// <para>
/// <b>Every kind is filtered through the live roster, including the explicit list.</b> The obvious
/// reading of <c>{"kind":"users","userIds":[…]}</c> is "these people, as given" — 40.21 stored them
/// unchecked precisely because it could not check them. Checking them here is not tidiness: the ids
/// were chosen in the admin panel at some earlier moment, and between then and pressing "issue" a
/// person can have left. Issuing to them writes a progress row that will read "not started" forever
/// and mails homework to somebody's old address. Filtering also closes the more serious hole, which
/// is that a hand-written body could name a user id belonging to a different organization: the
/// progress row would be written under this organization's id and stay isolated, but the person
/// would be notified about work at a company they do not work for.
/// </para>
///
/// <para>
/// <b>An audience that resolves to nobody is refused, not accepted as an empty fan-out.</b> A
/// silently empty issue is the failure this whole block exists to remove — the assignment goes
/// <c>active</c>, the funnel reads zero of zero, and the screen is indistinguishable from a team
/// that has not started.
/// </para>
/// </summary>
internal sealed class AssignmentAudienceResolver(
    IOrganizationMemberDirectory memberDirectory,
    ILogger<AssignmentAudienceResolver> logger) : IAssignmentAudienceResolver
{
    /// <summary>
    /// Turns one audience rule into the ids it currently means.
    ///
    /// <para>
    /// <b>A <c>group</c> audience is refused with a 400.</b> 40.21 accepted the kind structurally so
    /// this block would need no migration, and nothing in the platform defines a group yet. Refused
    /// rather than quietly widened to the whole team: "I sent it to the new hires" turning into "I sent
    /// it to everybody" is the kind of surprise that gets a product distrusted. When groups appear, this
    /// is the one <c>switch</c> to change.
    /// </para>
    ///
    /// <para>
    /// <b>An unreachable identity-service is translated, not allowed to bubble as a 500.</b> "We could
    /// not find out who works here" must never be reachable from the same code path as "nobody works
    /// here", and it must never look like a defect in the assignment the РОП just wrote.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<Guid>> ResolveAsync(
        AssignmentAudienceDto audience,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audience);

        if (audience.Kind == AssignmentAudienceKinds.Group)
        {
            throw new AssignmentValidationException(
                "Groups do not exist in the platform yet, so an assignment cannot be issued to one. "
                + "Use a list of people or the whole team.");
        }

        IReadOnlyList<Guid> activeMemberIds;
        try
        {
            activeMemberIds = (await memberDirectory.GetRosterAsync(cancellationToken)).MemberIds;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new AssignmentAudienceUnavailableException(
                "The list of people in this organization could not be read just now, so the assignment "
                + "was not issued to anybody. Nothing was changed — try again in a moment.",
                exception);
        }

        var resolved = audience.Kind switch
        {
            AssignmentAudienceKinds.WholeTeam => activeMemberIds,
            AssignmentAudienceKinds.Users => IntersectWithRoster(audience.UserIds, activeMemberIds, logger),
            _ => throw new AssignmentValidationException(
                $"'{audience.Kind}' is not a known audience kind."),
        };

        if (resolved.Count == 0)
        {
            throw new AssignmentValidationException(
                "This audience currently includes nobody who works here. Check the people named, "
                + "or issue it to the whole team.");
        }

        return resolved;
    }

    /// <summary>
    /// The named ids that are still on the roster.
    ///
    /// <para>
    /// A dropped id is logged rather than refused. The РОП asked for the people they can still reach,
    /// and failing the whole issue because one person left last week would make offboarding break every
    /// assignment that ever named them.
    /// </para>
    /// </summary>
    private static List<Guid> IntersectWithRoster(
        IReadOnlyList<Guid>? namedUserIds,
        IReadOnlyList<Guid> activeMemberIds,
        ILogger logger)
    {
        if (namedUserIds is null || namedUserIds.Count == 0)
        {
            return [];
        }

        var roster = activeMemberIds.ToHashSet();
        var distinctNamedUserIds = namedUserIds.Distinct().ToList();
        var resolved = distinctNamedUserIds.Where(roster.Contains).ToList();

        var droppedCount = distinctNamedUserIds.Count - resolved.Count;
        if (droppedCount > 0)
        {
            logger.LogInformation(
                "Assignment audience named {DroppedCount} user(s) with no active membership here; they were left out.",
                droppedCount);
        }

        return resolved;
    }
}
