namespace Sellevate.Learning.Features.Exercises.Constants;

/// <summary>
/// The JSON property names a learner's submitted answer is read by.
///
/// <para>
/// Deliberately separate from <see cref="ExerciseContentFields"/> even where the two share a spelling:
/// the answer payload is the client's half of the contract and the content document is the author's,
/// and they are free to diverge. The submitted body is stored verbatim on
/// <c>UserExerciseAttempt.SerializedAnswer</c>, so these names are as persisted as the content ones —
/// renaming one would make every stored attempt unreadable to a re-grade.
/// </para>
/// </summary>
public static class ExerciseAnswerFields
{
    public const string SelectedOptionIndex = "selectedOptionIndex";
    public const string Order = "order";
    public const string Pairs = "pairs";
    public const string Left = "left";
    public const string Right = "right";
    public const string Mapping = "mapping";
}
