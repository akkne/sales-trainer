namespace Sellevate.Organization.Features.Organizations.Models;

/// <summary>
/// Phase 40.29. One thing the profile still does not say, and the question that fills it in.
/// </summary>
/// <param name="Code">
/// One of <c>OrganizationProfileGapCodes</c>. The screen keys off this rather than off the sentence,
/// for 40.28's reason: codes can be counted, sorted, skipped and translated; a paragraph can only be
/// shown.
/// </param>
/// <param name="Question">The Russian question, resolved from the code on the server.</param>
/// <param name="Priority">
/// <c>blocking</c> / <c>important</c> / <c>optional</c>. <c>blocking</c> means
/// <c>{{organization.*}}</c> substitution renders the neutral fallback until it is answered — the
/// state where, in the roadmap's words, «параметризация базового контента не заработает вообще».
/// </param>
public sealed record OrganizationProfileGapDto(string Code, string Question, string Priority);
