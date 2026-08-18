using System.Text.Json;

namespace Sellevate.Ai.Features.ContentAdaptation.Models;

/// <summary>
/// Phase 40.32. What the rewriter came back with.
///
/// <para>
/// <b>Both fields nullable, and «ничего не меняю» is a first-class answer.</b> An exercise that is
/// already in the customer's voice must be allowed to come back untouched — a model that is required
/// to produce a change produces one, and a review queue padded with cosmetic rewrites of good
/// exercises is how a person learns to accept everything without reading it.
/// </para>
/// </summary>
/// <param name="Content">The rewritten body, or null when nothing needed changing.</param>
/// <param name="Summary">
/// One or two sentences saying what changed and why, in the customer's language. <b>The prose half
/// of the diff</b>: the field-level change list is computed on the server from the two documents,
/// and the one thing it can never supply is the reason.
/// </param>
public sealed record RewrittenExerciseDto(
    JsonElement? Content,
    string? Summary);
