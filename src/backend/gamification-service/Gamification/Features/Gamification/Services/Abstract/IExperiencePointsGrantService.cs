namespace Sellevate.Gamification.Features.Gamification.Services.Abstract;

/// <summary>
/// The only way experience points enter the ledger.
///
/// <para>
/// Pass <c>sourceEventId</c> whenever the grant originates in an integration event: it makes the
/// grant idempotent, which is what lets an at-least-once consumer retry safely. A grant of zero is a
/// no-op, not an error.
/// </para>
/// </summary>
public interface IExperiencePointsGrantService
{
    Task GrantAsync(Guid userId, int amount, string source, DateTime? earnedAt = null, Guid? sourceEventId = null, CancellationToken cancellationToken = default);
}
