namespace Sellevate.Learning.Features.ContentAdaptation.Models;

/// <summary>
/// Phase 40.32. One thing wrong with one exercise, as the review reports it.
///
/// <para>
/// <b>Codes on the wire, sentences on the server</b> — the arrangement 40.28 settled on for
/// refusals, applied to critique. ai-service returns <see cref="Code"/> and at most a short
/// <see cref="Detail"/> quoting the offending fragment; <see cref="Message"/> and
/// <see cref="Severity"/> are looked up from <c>ContentReviewFindingCodes</c> here. A model that
/// wrote the complaint itself would phrase it differently on every run, and nobody could ever count
/// how many of a customer's exercises have the same defect.
/// </para>
/// </summary>
/// <param name="Code">One of <c>ContentReviewFindingCodes</c>. Unknown codes are dropped, never rendered blank.</param>
/// <param name="Severity"><c>blocking</c> or <c>advisory</c>, from the code and not from the model.</param>
/// <param name="Message">The fixed Russian sentence the РОП reads.</param>
/// <param name="Detail">The offending fragment in the exercise's own words, when the model quoted one.</param>
public sealed record ContentReviewFindingDto(
    string Code,
    string Severity,
    string Message,
    string? Detail);
