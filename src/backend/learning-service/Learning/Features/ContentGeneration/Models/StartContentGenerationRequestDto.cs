namespace Sellevate.Learning.Features.ContentGeneration.Models;

/// <summary>
/// Phase 40.27. Start a run: a name and the material. Nothing about who it is for and nothing about
/// what to generate — those are questions for after the structure is on the screen.
/// </summary>
/// <param name="Title">
/// What the РОП calls this training. Becomes the generated topic's title, so it is worth asking for
/// rather than deriving: «Возражения по цене, октябрь» is a thing somebody finds again.
/// </param>
/// <param name="Material">
/// The raw text — a deck's contents, a call script, notes. Pasted rather than uploaded: file parsing
/// (and the call recordings that make it worth building) is roadmap 40.30.
/// </param>
public sealed record StartContentGenerationRequestDto(string Title, string Material);
