namespace Sellevate.Learning.Features.ContentGeneration.Models;

/// <summary>
/// Phase 40.28. The pipeline refuses to generate from this, and says what is missing.
///
/// <para>
/// <b>It carries the gap list, and that is the whole point.</b> A refusal that reaches the РОП as a
/// bare 400 with a sentence is a refusal about them; one that names «нет ни одного возражения —
/// добавьте примеры или запись звонка» is a refusal they can act on in five minutes. The
/// <see cref="Insufficiency"/> is also written to the run before this is thrown, so a screen that
/// polls the run sees the same list without having caught anything.
/// </para>
///
/// <para>
/// Separate from <c>ContentGenerationValidationException</c> (400 — the request was malformed) and
/// from <c>ContentGenerationStateException</c> (409 — the caller is merely late): here the request was
/// well-formed, the state was right, and the answer is still no.
/// </para>
/// </summary>
public sealed class ContentGenerationInsufficientMaterialException(
    string message,
    ContentInsufficiencyDto insufficiency) : Exception(message)
{
    public ContentInsufficiencyDto Insufficiency { get; } = insufficiency;
}
