namespace Sellevate.Learning.Features.Exercises.Constants;

/// <summary>
/// The JSON property names inside an exercise's stored content document, per
/// docs/NEW_EXERCISE_TYPES.md.
///
/// <para>
/// <b>These names are a three-way contract, which is why they live in one place.</b>
/// <c>ExerciseContentValidator</c> decides a body is playable by them, <c>ExerciseService</c> strips
/// the answer-key ones before the body reaches the learner, and the per-type evaluation strategies
/// grade by them. A name that drifts in one of the three either leaks the answer to the client or
/// makes a correct answer score zero, and both failures are silent.
/// </para>
///
/// <para>
/// They are also persisted: every exercise row and every frozen <c>LessonVersion</c> snapshot in the
/// database already uses them, and the frontend editors mirror them. Add names here; never change
/// one.
/// </para>
/// </summary>
public static class ExerciseContentFields
{
    /// <summary>
    /// Explanation shown after grading. Optional on every type — its absence means "no explanation
    /// authored", never "explanation empty".
    /// </summary>
    public const string Explanation = "explanation";

    public const string Situation = "situation";
    public const string Instruction = "instruction";
    public const string Original = "original";
    public const string Text = "text";
    public const string Body = "body";

    public const string Options = "options";
    public const string Items = "items";
    public const string Pairs = "pairs";
    public const string Left = "left";
    public const string Right = "right";
    public const string Categories = "categories";
    public const string Turns = "turns";
    public const string Side = "side";

    public const string Before = "before";
    public const string After = "after";

    public const string Dialogue = "dialogue";
    public const string Speaker = "speaker";

    public const string Transcript = "transcript";
    public const string EvaluationAxes = "evaluation_axes";
    public const string Name = "name";
    public const string Description = "description";

    public const string Persona = "persona";
    public const string Scenario = "scenario";
    public const string Context = "context";
    public const string MaximumTurns = "max_turns";

    public const string Layout = "layout";

    /// <summary>
    /// Answer-key fields. Every one of these must be stripped from a body before it is sent to a
    /// learner — <c>ExerciseService.StripAnswerKeyFields</c> is the only place that is done, and a
    /// field added here without being added there is a leaked answer.
    /// </summary>
    public const string IsCorrect = "is_correct";

    /// <inheritdoc cref="IsCorrect"/>
    public const string CorrectPosition = "correct_position";

    /// <inheritdoc cref="IsCorrect"/>
    public const string Category = "category";

    /// <inheritdoc cref="IsCorrect"/>
    public const string IsMistake = "is_mistake";

    /// <inheritdoc cref="IsCorrect"/>
    public const string AiPrompt = "ai_prompt";
}
