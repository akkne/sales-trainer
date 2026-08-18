using Sellevate.Learning.Features.Assignments.Models;

namespace Sellevate.Learning.Features.Assignments.Services.Abstract;

/// <summary>
/// Phase 40.23. Turns an audience <b>rule</b> into the named people it currently means
/// (docs/TENANCY/ASSIGNMENTS.md §1, roadmap 40.23 "Аудитория").
/// </summary>
public interface IAssignmentAudienceResolver
{
    /// <summary>
    /// The user ids the rule resolves to, right now, in the organization in context.
    ///
    /// <para>
    /// Throws <see cref="AssignmentValidationException"/> when the rule names nobody who works here
    /// — an assignment issued to an empty audience is a draft the РОП believes they issued — and
    /// propagates whatever identity-service's failure was when the roster cannot be read at all.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Guid>> ResolveAsync(
        AssignmentAudienceDto audience,
        CancellationToken cancellationToken = default);
}
