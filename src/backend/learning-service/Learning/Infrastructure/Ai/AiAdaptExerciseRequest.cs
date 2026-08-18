using System.Text.Json;
using Sellevate.Learning.Features.ContentGeneration.Models;

namespace Sellevate.Learning.Infrastructure.Ai;

/// <summary>
/// Phase 40.32. One exercise plus the customer's profile — the request shape of both per-exercise
/// calls, rewrite and review.
///
/// <para>
/// The profile travels as <see cref="ContentStructureDto"/>, the record 40.27 already uses for the
/// same seven fields, rather than as a second shape meaning the same thing. It carries the tone the
/// rewrite aims for and the banned claims that bind both halves.
/// </para>
/// </summary>
public sealed record AiAdaptExerciseRequest(
    string ExerciseType,
    JsonElement Content,
    ContentStructureDto? Profile);
