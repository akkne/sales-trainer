namespace Sellevate.Learning.Features.ContentAdaptation.Models;

/// <summary>
/// Phase 40.32. «Перепиши все упражнения этапа "закрытие"», as a request body.
///
/// <para>
/// Two fields, and neither of them is a list of exercises. The РОП names a stage and a purpose; the
/// server decides what is in scope, because that decision has to go through 40.18's read resolution
/// to avoid proposing a rewrite of a base lesson the organization has already overridden. A caller
/// who could supply exercise ids could bypass that and rewrite the wrong half of a fork.
/// </para>
/// </summary>
/// <param name="Mode">One of <c>ContentAdaptationModes</c>. Defaults to the tone rewrite when omitted.</param>
/// <param name="StageKey">A <c>Skill.Stage</c> value, e.g. <c>closing</c>.</param>
public sealed record StartContentAdaptationRequestDto(
    string? Mode,
    string StageKey);
