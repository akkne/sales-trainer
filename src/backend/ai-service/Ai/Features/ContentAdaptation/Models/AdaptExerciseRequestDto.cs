using System.Text.Json;
using Sellevate.Ai.Features.ContentGeneration.Models;

namespace Sellevate.Ai.Features.ContentAdaptation.Models;

/// <summary>
/// Phase 40.32. One exercise, plus who the customer is. The request shape of both halves of the
/// block — rewriting an exercise into a customer's voice, and reviewing one they wrote themselves.
///
/// <para>
/// <b>One exercise per call, not a stage per call.</b> A stage is up to sixty exercises; asking for
/// all of them in one completion means one truncation loses the lot, one bad exercise poisons the
/// whole answer, and a batch interrupted at exercise forty starts again at one. Per-exercise calls
/// cost more requests and buy the property the block actually needs: the unit of payment, the unit
/// of failure and the unit a person accepts are the same row.
/// </para>
///
/// <para>
/// <b>The profile is the same record the pipeline already passes around</b>
/// (<see cref="ExtractedContentStructureDto"/>) rather than a second shape meaning the same thing.
/// It is what carries the tone the rewrite is aiming for and — the part that matters most — the
/// banned claims, which bind both halves: the rewriter must never produce one, and the reviewer
/// reports an exercise whose correct answer rewards one.
/// </para>
/// </summary>
/// <param name="ExerciseType">One of the product's exercise types. The answer must keep it.</param>
/// <param name="Content">The exercise body as it stands today.</param>
/// <param name="Profile">The organization's product, ICP, tone, glossary and banned claims. Optional.</param>
public sealed record AdaptExerciseRequestDto(
    string ExerciseType,
    JsonElement Content,
    ExtractedContentStructureDto? Profile = null);
