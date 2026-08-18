using System.Text.Json;

namespace Sellevate.Learning.Infrastructure.Ai;

/// <summary>
/// Phase 40.32. What the rewriter came back with: a proposed body, or nothing at all.
///
/// <para>
/// A null <see cref="Content"/> means «переписывать нечего» and is a first-class answer, not a
/// failure — the item resolves as <c>unchanged</c> without ever reaching a person's queue.
/// </para>
/// </summary>
public sealed record AiRewrittenExercise(JsonElement? Content, string? Summary);
