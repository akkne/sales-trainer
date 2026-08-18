using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Exercises.Constants;
using Sellevate.Learning.Features.Exercises.Services.Implementation;

namespace Sellevate.Learning.Tests.Unit;

/// <summary>
/// <c>ExerciseService.StripAnswerKeyFields</c> is the only place an exercise's answer is withheld from
/// the learner, and it withholds by naming each type explicitly. A type that falls through its switch
/// gets its body returned untouched — correct for the types whose grading is AI-side or symmetric, and
/// a silent leak for any type added to <see cref="ExerciseTypes"/> and forgotten there.
///
/// <para>
/// These tests exist to make "forgotten" loud. The last one is the tripwire: every entry in
/// <c>ExerciseTypes.All</c> must appear either in the mapped set or in the deliberately-nothing-to-hide
/// set, so adding an eleventh type without deciding which it is fails here instead of shipping
/// <c>is_correct</c> to the client.
/// </para>
///
/// <para>
/// The method is private and static, so it is reached by reflection. That is deliberate: the public
/// path needs a database context and a profile provider, and this behaviour is worth pinning on its own
/// rather than through two collaborators. A rename breaks these tests loudly, which is the correct
/// outcome for a method whose job is withholding an answer.
/// </para>
/// </summary>
[TestFixture]
public sealed class ExerciseAnswerKeyStrippingTests
{
    /// <summary>
    /// Types whose learner-facing body is deliberately identical to the stored one. Each has nothing to
    /// hide: grading is either AI-side (the model receives the transcript and judges it) or symmetric
    /// (the learner is shown exactly what the grader compares against). Adding a type here is a
    /// decision, which is the point — it cannot happen by omission.
    /// </summary>
    private static readonly string[] TypesWithNothingToHide =
    [
        ExerciseTypes.MatchPairs,
        ExerciseTypes.Rewrite,
        ExerciseTypes.FreeText,
        ExerciseTypes.EvaluateCall,
        ExerciseTypes.TheoryCard,
    ];

    /// <summary>The answer-key field each mapped type must remove, and where it lives.</summary>
    private static readonly (string ExerciseType, string ContainerField, string? AnswerKeyField)[] MappedTypes =
    [
        (ExerciseTypes.ChooseOption, ExerciseContentFields.Options, ExerciseContentFields.IsCorrect),
        (ExerciseTypes.FillBlank, ExerciseContentFields.Options, ExerciseContentFields.IsCorrect),
        (ExerciseTypes.Reorder, ExerciseContentFields.Items, ExerciseContentFields.CorrectPosition),
        (ExerciseTypes.Categorize, ExerciseContentFields.Items, ExerciseContentFields.Category),
        (ExerciseTypes.SpotMistake, ExerciseContentFields.Dialogue, ExerciseContentFields.IsMistake),
        (ExerciseTypes.AiDialogue, ContainerField: null!, ExerciseContentFields.AiPrompt),
    ];

    private static readonly MethodInfo StripAnswerKeyFields =
        typeof(ExerciseService).GetMethod(
            "StripAnswerKeyFields",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            "ExerciseService.StripAnswerKeyFields was renamed or removed. It is the only place an "
            + "exercise's answer key is withheld from the learner — find where that now happens and "
            + "point these tests at it rather than deleting them.");

    /// <summary>
    /// The parsed document is deliberately not disposed. For a type with nothing to hide the method
    /// returns the caller's own <see cref="JsonElement"/>, which is a read-only view over its parent
    /// document — disposing the document would invalidate the very value under test. Production has the
    /// same shape: <c>ExerciseService</c> parses the rendered content and hands the element to the DTO
    /// without disposing it.
    /// </summary>
    private static JsonElement Strip(string exerciseType, string contentJson)
    {
        var document = JsonDocument.Parse(contentJson);
        return (JsonElement)StripAnswerKeyFields.Invoke(null, [exerciseType, document.RootElement])!;
    }

    /// <summary>
    /// A body carrying every answer-key marker at once, so one input can be sent through every type.
    /// </summary>
    private const string BodyWithEveryAnswerKey = """
        {
          "instruction": "Выберите верный вариант",
          "ai_prompt": "Играй скептичного закупщика и не соглашайся сразу",
          "options": [
            { "text": "Первый", "is_correct": true },
            { "text": "Второй", "is_correct": false }
          ],
          "items": [
            { "text": "Шаг", "correct_position": 2, "category": "открытие" }
          ],
          "dialogue": [
            { "speaker": "me", "text": "Реплика", "is_mistake": true }
          ],
          "pairs": [ { "left": "A", "right": "Б" } ]
        }
        """;

    private static IEnumerable<TestCaseData> MappedTypeCases() =>
        MappedTypes.Select(mapping => new TestCaseData(
                mapping.ExerciseType, mapping.ContainerField, mapping.AnswerKeyField)
            .SetName($"The answer key is stripped for {mapping.ExerciseType}"));

    [TestCaseSource(nameof(MappedTypeCases))]
    public void A_mapped_type_never_ships_its_answer_key(
        string exerciseType, string? containerField, string answerKeyField)
    {
        var learnerContent = Strip(exerciseType, BodyWithEveryAnswerKey);

        if (containerField is null)
        {
            learnerContent.TryGetProperty(answerKeyField, out _).Should().BeFalse(
                $"'{answerKeyField}' is the answer key of {exerciseType} and must not reach the learner");
            return;
        }

        var container = learnerContent.GetProperty(containerField);
        foreach (var item in container.EnumerateArray())
        {
            item.TryGetProperty(answerKeyField, out _).Should().BeFalse(
                $"'{answerKeyField}' is the answer key of {exerciseType} and must not reach the learner");
        }
    }

    /// <summary>
    /// Stripping removes fields and never rewrites them: everything the learner legitimately needs
    /// survives untouched, which is also what lets organization placeholder rendering run before this.
    /// </summary>
    [Test]
    public void Stripping_removes_only_the_answer_key_and_copies_everything_else_through()
    {
        var learnerContent = Strip(ExerciseTypes.ChooseOption, BodyWithEveryAnswerKey);

        learnerContent.GetProperty(ExerciseContentFields.Instruction).GetString()
            .Should().Be("Выберите верный вариант");

        var options = learnerContent.GetProperty(ExerciseContentFields.Options).EnumerateArray().ToList();
        options.Should().HaveCount(2);
        options[0].GetProperty(ExerciseContentFields.Text).GetString().Should().Be("Первый");

        learnerContent.GetProperty(ExerciseContentFields.Pairs).EnumerateArray()
            .Should().HaveCount(1);
    }

    /// <summary>
    /// Only the named container is walked. <c>choose_option</c> strips <c>is_correct</c> from options and
    /// must not reach into <c>dialogue</c> or <c>items</c> — otherwise a body carrying several shapes
    /// would lose fields the learner needs.
    /// </summary>
    [Test]
    public void Stripping_touches_only_the_container_the_type_names()
    {
        var learnerContent = Strip(ExerciseTypes.ChooseOption, BodyWithEveryAnswerKey);

        learnerContent.GetProperty(ExerciseContentFields.Items).EnumerateArray().Single()
            .TryGetProperty(ExerciseContentFields.CorrectPosition, out _).Should().BeTrue();
        learnerContent.GetProperty(ExerciseContentFields.Dialogue).EnumerateArray().Single()
            .TryGetProperty(ExerciseContentFields.IsMistake, out _).Should().BeTrue();
    }

    [TestCaseSource(nameof(TypesWithNothingToHide))]
    public void A_type_with_nothing_to_hide_is_returned_unchanged(string exerciseType)
    {
        var learnerContent = Strip(exerciseType, BodyWithEveryAnswerKey);

        JsonSerializer.Serialize(learnerContent).Should().Be(
            JsonSerializer.Serialize(JsonDocument.Parse(BodyWithEveryAnswerKey).RootElement),
            "these types grade AI-side or symmetrically, so the learner may see the whole body");
    }

    [Test]
    public void A_body_missing_the_container_entirely_is_handled_rather_than_throwing()
    {
        var act = () => Strip(ExerciseTypes.ChooseOption, """{ "instruction": "Только текст" }""");

        act.Should().NotThrow();
        act().TryGetProperty(ExerciseContentFields.Options, out _).Should().BeFalse();
    }

    [Test]
    public void A_container_that_is_not_an_array_is_left_alone_rather_than_throwing()
    {
        var act = () => Strip(ExerciseTypes.Reorder,
            $$"""{ "{{ExerciseContentFields.Items}}": "не массив" }""");

        act.Should().NotThrow();
        act().GetProperty(ExerciseContentFields.Items).GetString().Should().Be("не массив");
    }

    /// <summary>
    /// <b>The tripwire.</b> Every exercise type must be a deliberate member of exactly one of the two
    /// sets. An eleventh type added to <c>ExerciseTypes.All</c> and forgotten in the strip switch would
    /// otherwise ship its own answer key to the client — silently, with nothing else in the suite or in
    /// either linter noticing.
    ///
    /// <para>
    /// If this fails after a type was added: decide whether the new type has an answer to withhold. If
    /// it does, map it in <c>StripAnswerKeyFields</c> and add it to <c>MappedTypes</c> here. If it does
    /// not, add it to <c>TypesWithNothingToHide</c> — and that addition is the record of somebody having
    /// made the decision.
    /// </para>
    /// </summary>
    [Test]
    public void Every_exercise_type_is_either_mapped_or_deliberately_exempt()
    {
        var mapped = MappedTypes.Select(mapping => mapping.ExerciseType).ToHashSet(StringComparer.Ordinal);
        var exempt = TypesWithNothingToHide.ToHashSet(StringComparer.Ordinal);

        mapped.Overlaps(exempt).Should().BeFalse(
            "a type cannot both hide an answer key and have nothing to hide");

        mapped.Union(exempt).Should().BeEquivalentTo(ExerciseTypes.All,
            "a type in neither set falls through the strip switch and ships its answer key to the "
            + "learner; see this test's remarks for which set to add it to");
    }
}
