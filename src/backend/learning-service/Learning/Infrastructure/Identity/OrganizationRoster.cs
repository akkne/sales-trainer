namespace Sellevate.Learning.Infrastructure.Identity;

/// <summary>
/// Phase 40.26. One answer to the two questions learning-service has to ask identity-service about
/// an organization: who works here, and who runs it.
///
/// <para>
/// <b>One record because it is one HTTP call.</b> Every caller that needs the administrators also
/// needs the roster in the same breath — the deadline digest names the people who have not started
/// (roster) and addresses the РОП (administrators) — and two methods would have meant two
/// round-trips with two independent failure modes for facts that must agree with each other.
/// </para>
/// </summary>
/// <param name="MemberIds">
/// Every active membership. Phase 40.23's original answer, unchanged: fail-closed callers write
/// progress rows from it, so "we could not find out" is raised rather than returned empty.
/// </param>
/// <param name="AdministratorIds">
/// The subset holding a tenancy administrator role, or <see langword="null"/> when identity-service
/// did not report administrators at all.
///
/// <para>
/// <b>The null is a version skew, not an empty organization, and the difference matters.</b> An
/// identity-service older than 40.26 answers this route without the field; an organization with no
/// administrator answers it with an empty list. Collapsing the two would make a rolling deploy
/// silently swallow one digest per assignment and leave nothing behind to notice — the failure shape
/// docs/TENANCY/BACKGROUND_JOBS.md exists to keep out of this codebase. Callers that address
/// administrators therefore treat <see langword="null"/> as "ask again next tick".
/// </para>
/// </param>
public sealed record OrganizationRoster(
    IReadOnlyList<Guid> MemberIds,
    IReadOnlyList<Guid>? AdministratorIds);
