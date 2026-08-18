namespace Sellevate.Learning.Features.TeamInsights.Models;

/// <summary>
/// Phase 40.31. The caller asked to act on a gap the measurement does not currently show.
///
/// <para>
/// A 409 rather than a 404 or a 400: the stage exists, the request was well formed, and the caller
/// was not wrong to ask — the team simply is not failing there any more, most likely because the
/// panel they are looking at was drawn twenty minutes ago. The message says which, so the screen can
/// refresh rather than apologise.
/// </para>
/// </summary>
public sealed class TeamSkillGapStateException(string message) : Exception(message);
