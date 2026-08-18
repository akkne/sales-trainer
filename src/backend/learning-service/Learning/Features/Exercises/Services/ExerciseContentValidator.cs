using System.Text.Json;

using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.Exercises.Constants;

namespace Sellevate.Learning.Features.Exercises.Services;

/// <summary>
/// Decides whether an exercise body is playable, per-type, against the schemas in
/// docs/NEW_EXERCISE_TYPES.md.
///
/// <para>
/// <b>A false accept is a blank screen mid-lesson, so every rule here is a rejection rule.</b> The
/// player has no fallback for a body that parses as JSON but lacks the fields it renders: the learner
/// simply sees nothing. That makes silent coercion the one thing this class must never do — an
/// unrecognized field is left alone, but a missing or wrong-typed required field is always an error,
/// never a defaulted value. Every caller (the admin editors, the seeder, generated content, and
/// adaptation proposals) gates on the returned list being empty.
/// </para>
///
/// <para>
/// <b>It reports every problem it finds, not the first.</b> An author fixing an imported lesson needs
/// the whole list in one pass; a walk that returned on first failure would turn one bad import into a
/// dozen round trips. Where a container is missing or the wrong kind, the walk into its children is
/// skipped — those would be noise about a structure that does not exist — but sibling checks continue.
/// </para>
///
/// <para>
/// <b>Type keys come from <see cref="ExerciseTypes"/> and are persisted and compared in SQL.</b> An
/// unknown type is rejected with the valid set spelled out, so adding a type means adding it there,
/// adding a <c>Validate…</c> branch here, and giving it an evaluation strategy — a type accepted here
/// with no strategy behind it is the same blank screen by another route.
/// </para>
/// </summary>
public static class ExerciseContentValidator
{
    private const string TheoryCardLayoutText = "text";
    private const string TheoryCardLayoutDialogue = "dialogue";
    private const string TheoryCardLayoutBullets = "bullets";
    private const string TheoryCardLayoutQuote = "quote";

    private const string DialogueSideMe = "me";
    private const string DialogueSideThem = "them";

    private static readonly string[] TheoryCardLayouts =
    [
        TheoryCardLayoutText,
        TheoryCardLayoutDialogue,
        TheoryCardLayoutBullets,
        TheoryCardLayoutQuote,
    ];

    /// <summary>
    /// Every reason <paramref name="content"/> cannot be played as an exercise of
    /// <paramref name="type"/>. An empty list means accepted; the list is never <see langword="null"/>.
    /// </summary>
    public static IReadOnlyList<string> Validate(string type, JsonElement content)
    {
        var errors = new List<string>();

        if (content.ValueKind != JsonValueKind.Object)
        {
            errors.Add("content must be a JSON object.");
            return errors;
        }

        switch (type)
        {
            case ExerciseTypes.ChooseOption:
                ValidateChooseOption(content, errors);
                break;
            case ExerciseTypes.FillBlank:
                ValidateFillBlank(content, errors);
                break;
            case ExerciseTypes.Reorder:
                ValidateReorder(content, errors);
                break;
            case ExerciseTypes.MatchPairs:
                ValidateMatchPairs(content, errors);
                break;
            case ExerciseTypes.Categorize:
                ValidateCategorize(content, errors);
                break;
            case ExerciseTypes.SpotMistake:
                ValidateSpotMistake(content, errors);
                break;
            case ExerciseTypes.Rewrite:
                ValidateRewrite(content, errors);
                break;
            case ExerciseTypes.AiDialogue:
                ValidateAiDialogue(content, errors);
                break;
            case ExerciseTypes.EvaluateCall:
                ValidateEvaluateCall(content, errors);
                break;
            case ExerciseTypes.FreeText:
                ValidateFreeText(content, errors);
                break;
            case ExerciseTypes.TheoryCard:
                ValidateTheoryCard(content, errors);
                break;
            default:
                errors.Add($"Unknown exercise type '{type}'. Valid types: {string.Join(", ", ExerciseTypes.All)}.");
                break;
        }

        return errors;
    }

    private static void ValidateChooseOption(JsonElement root, List<string> errors)
    {
        RequireNonEmptyString(root, ExerciseContentFields.Situation, errors);
        var options = RequireArray(root, ExerciseContentFields.Options, errors);
        if (options is not null)
        {
            if (options.Value.GetArrayLength() < 2)
                errors.Add("options must contain at least 2 items.");
            ValidateOptionsArray(options.Value, errors);
        }
    }

    private static void ValidateFillBlank(JsonElement root, List<string> errors)
    {
        RequireString(root, ExerciseContentFields.Before, errors);
        RequireString(root, ExerciseContentFields.After, errors);
        var options = RequireArray(root, ExerciseContentFields.Options, errors);
        if (options is not null)
        {
            if (options.Value.GetArrayLength() < 2)
                errors.Add("options must contain at least 2 items.");
            ValidateOptionsArray(options.Value, errors);
        }
    }

    private static void ValidateReorder(JsonElement root, List<string> errors)
    {
        RequireNonEmptyString(root, ExerciseContentFields.Instruction, errors);
        var items = RequireArray(root, ExerciseContentFields.Items, errors);
        if (items is null) return;

        if (items.Value.GetArrayLength() < 2)
        {
            errors.Add("items must contain at least 2 items.");
            return;
        }

        var positions = new List<int>();
        var index = 0;
        foreach (var item in items.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"items[{index}] must be an object.");
                index++;
                continue;
            }
            if (!item.TryGetProperty(ExerciseContentFields.Text, out var textValue) || textValue.ValueKind != JsonValueKind.String)
                errors.Add($"items[{index}].text must be a string.");
            if (!item.TryGetProperty(ExerciseContentFields.CorrectPosition, out var positionValue) || positionValue.ValueKind != JsonValueKind.Number)
                errors.Add($"items[{index}].correct_position must be an integer.");
            else
                positions.Add(positionValue.GetInt32());
            index++;
        }

        if (positions.Count != positions.Distinct().Count())
            errors.Add("correct_position values in items must be unique.");
    }

    private static void ValidateMatchPairs(JsonElement root, List<string> errors)
    {
        RequireNonEmptyString(root, ExerciseContentFields.Instruction, errors);
        var pairs = RequireArray(root, ExerciseContentFields.Pairs, errors);
        if (pairs is null) return;

        if (pairs.Value.GetArrayLength() < 2)
        {
            errors.Add("pairs must contain at least 2 items.");
            return;
        }

        var index = 0;
        foreach (var pair in pairs.Value.EnumerateArray())
        {
            if (pair.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"pairs[{index}] must be an object.");
                index++;
                continue;
            }
            if (!pair.TryGetProperty(ExerciseContentFields.Left, out var left) || left.ValueKind != JsonValueKind.String)
                errors.Add($"pairs[{index}].left must be a string.");
            if (!pair.TryGetProperty(ExerciseContentFields.Right, out var right) || right.ValueKind != JsonValueKind.String)
                errors.Add($"pairs[{index}].right must be a string.");
            index++;
        }
    }

    private static void ValidateCategorize(JsonElement root, List<string> errors)
    {
        RequireNonEmptyString(root, ExerciseContentFields.Instruction, errors);

        var categoriesElement = RequireArray(root, ExerciseContentFields.Categories, errors);
        var categories = new HashSet<string>(StringComparer.Ordinal);
        if (categoriesElement is not null)
        {
            if (categoriesElement.Value.GetArrayLength() < 2)
                errors.Add("categories must contain at least 2 items.");

            var categoryIndex = 0;
            foreach (var category in categoriesElement.Value.EnumerateArray())
            {
                if (category.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(category.GetString()))
                    errors.Add($"categories[{categoryIndex}] must be a non-empty string.");
                else
                    categories.Add(category.GetString()!);
                categoryIndex++;
            }
        }

        var itemsElement = RequireArray(root, ExerciseContentFields.Items, errors);
        if (itemsElement is null) return;

        if (itemsElement.Value.GetArrayLength() < 1)
        {
            errors.Add("items must contain at least 1 item.");
            return;
        }

        var index = 0;
        foreach (var item in itemsElement.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"items[{index}] must be an object.");
                index++;
                continue;
            }
            if (!item.TryGetProperty(ExerciseContentFields.Text, out var textValue) || textValue.ValueKind != JsonValueKind.String)
                errors.Add($"items[{index}].text must be a string.");
            if (!item.TryGetProperty(ExerciseContentFields.Category, out var categoryValue) || categoryValue.ValueKind != JsonValueKind.String)
                errors.Add($"items[{index}].category must be a string.");
            else if (categories.Count > 0 && !categories.Contains(categoryValue.GetString()!))
                errors.Add($"items[{index}].category '{categoryValue.GetString()}' is not one of the declared categories.");
            index++;
        }
    }

    private static void ValidateSpotMistake(JsonElement root, List<string> errors)
    {
        var dialogue = RequireArray(root, ExerciseContentFields.Dialogue, errors);
        if (dialogue is null) return;

        if (dialogue.Value.GetArrayLength() < 2)
        {
            errors.Add("dialogue must contain at least 2 items.");
            return;
        }

        var mistakeCount = 0;
        var index = 0;
        foreach (var line in dialogue.Value.EnumerateArray())
        {
            if (line.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"dialogue[{index}] must be an object.");
                index++;
                continue;
            }
            if (!line.TryGetProperty(ExerciseContentFields.Speaker, out var speaker) || speaker.ValueKind != JsonValueKind.String)
                errors.Add($"dialogue[{index}].speaker must be a string.");
            if (!line.TryGetProperty(ExerciseContentFields.Text, out var text) || text.ValueKind != JsonValueKind.String)
                errors.Add($"dialogue[{index}].text must be a string.");
            if (!line.TryGetProperty(ExerciseContentFields.IsMistake, out var isMistake) || isMistake.ValueKind != JsonValueKind.True && isMistake.ValueKind != JsonValueKind.False)
                errors.Add($"dialogue[{index}].is_mistake must be a boolean.");
            else if (isMistake.GetBoolean())
                mistakeCount++;
            index++;
        }

        if (mistakeCount == 0)
            errors.Add("dialogue must have exactly one item with is_mistake: true (found 0).");
        else if (mistakeCount > 1)
            errors.Add($"dialogue must have exactly one item with is_mistake: true (found {mistakeCount}).");
    }

    private static void ValidateRewrite(JsonElement root, List<string> errors)
    {
        RequireNonEmptyString(root, ExerciseContentFields.Instruction, errors);
        RequireNonEmptyString(root, ExerciseContentFields.Original, errors);
    }

    private static void ValidateAiDialogue(JsonElement root, List<string> errors)
    {
        RequireNonEmptyString(root, ExerciseContentFields.Persona, errors);
        RequireNonEmptyString(root, ExerciseContentFields.Scenario, errors);

        if (root.TryGetProperty(ExerciseContentFields.MaximumTurns, out var maxTurns))
        {
            if (maxTurns.ValueKind != JsonValueKind.Number)
                errors.Add("max_turns must be an integer.");
            else if (maxTurns.GetInt32() < 1)
                errors.Add("max_turns must be at least 1.");
        }
    }

    private static void ValidateEvaluateCall(JsonElement root, List<string> errors)
    {
        var transcript = RequireArray(root, ExerciseContentFields.Transcript, errors);
        if (transcript is not null)
        {
            if (transcript.Value.GetArrayLength() < 1)
                errors.Add("transcript must contain at least 1 item.");
            else
            {
                var index = 0;
                foreach (var line in transcript.Value.EnumerateArray())
                {
                    if (line.ValueKind != JsonValueKind.Object)
                    {
                        errors.Add($"transcript[{index}] must be an object.");
                        index++;
                        continue;
                    }
                    if (!line.TryGetProperty(ExerciseContentFields.Speaker, out var speaker) || speaker.ValueKind != JsonValueKind.String)
                        errors.Add($"transcript[{index}].speaker must be a string.");
                    if (!line.TryGetProperty(ExerciseContentFields.Text, out var text) || text.ValueKind != JsonValueKind.String)
                        errors.Add($"transcript[{index}].text must be a string.");
                    index++;
                }
            }
        }

        var axes = RequireArray(root, ExerciseContentFields.EvaluationAxes, errors);
        if (axes is not null)
        {
            if (axes.Value.GetArrayLength() < 1)
                errors.Add("evaluation_axes must contain at least 1 item.");
            else
            {
                var index = 0;
                foreach (var axis in axes.Value.EnumerateArray())
                {
                    if (axis.ValueKind != JsonValueKind.Object)
                    {
                        errors.Add($"evaluation_axes[{index}] must be an object.");
                        index++;
                        continue;
                    }
                    if (!axis.TryGetProperty(ExerciseContentFields.Name, out var name) || name.ValueKind != JsonValueKind.String)
                        errors.Add($"evaluation_axes[{index}].name must be a string.");
                    if (!axis.TryGetProperty(ExerciseContentFields.Description, out var descriptionValue) || descriptionValue.ValueKind != JsonValueKind.String)
                        errors.Add($"evaluation_axes[{index}].description must be a string.");
                    index++;
                }
            }
        }
    }

    private static void ValidateFreeText(JsonElement root, List<string> errors)
    {
        RequireNonEmptyString(root, ExerciseContentFields.Instruction, errors);
    }

    /// <summary>
    /// Theory cards are not graded, so the only thing that can be wrong with one is that it cannot be
    /// rendered. <c>layout</c> is the discriminator: each value demands a different set of fields, and
    /// the fields of the other layouts are irrelevant rather than forbidden, so a card that carries
    /// leftovers from an earlier layout still validates.
    /// </summary>
    private static void ValidateTheoryCard(JsonElement root, List<string> errors)
    {
        if (!root.TryGetProperty(ExerciseContentFields.Layout, out var layoutElement) || layoutElement.ValueKind != JsonValueKind.String)
        {
            errors.Add($"'layout' is required and must be one of: {string.Join(", ", TheoryCardLayouts)}.");
            return;
        }

        var layout = layoutElement.GetString();
        switch (layout)
        {
            case TheoryCardLayoutText:
                RequireNonEmptyString(root, ExerciseContentFields.Body, errors);
                break;

            case TheoryCardLayoutDialogue:
            {
                var turns = RequireArray(root, ExerciseContentFields.Turns, errors);
                if (turns is null) break;
                if (turns.Value.GetArrayLength() < 1)
                {
                    errors.Add("turns must contain at least 1 item.");
                    break;
                }
                var index = 0;
                foreach (var turn in turns.Value.EnumerateArray())
                {
                    if (turn.ValueKind != JsonValueKind.Object)
                    {
                        errors.Add($"turns[{index}] must be an object.");
                        index++;
                        continue;
                    }
                    if (!turn.TryGetProperty(ExerciseContentFields.Side, out var side) || side.ValueKind != JsonValueKind.String
                        || (side.GetString() != DialogueSideMe && side.GetString() != DialogueSideThem))
                        errors.Add($"turns[{index}].side must be \"{DialogueSideMe}\" or \"{DialogueSideThem}\".");
                    if (!turn.TryGetProperty(ExerciseContentFields.Text, out var text) || text.ValueKind != JsonValueKind.String
                        || string.IsNullOrWhiteSpace(text.GetString()))
                        errors.Add($"turns[{index}].text must be a non-empty string.");
                    index++;
                }
                break;
            }

            case TheoryCardLayoutBullets:
            {
                var items = RequireArray(root, ExerciseContentFields.Items, errors);
                if (items is null) break;
                if (items.Value.GetArrayLength() < 1)
                {
                    errors.Add("items must contain at least 1 item.");
                    break;
                }
                var index = 0;
                foreach (var item in items.Value.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
                        errors.Add($"items[{index}] must be a non-empty string.");
                    index++;
                }
                break;
            }

            case TheoryCardLayoutQuote:
                RequireNonEmptyString(root, ExerciseContentFields.Text, errors);
                break;

            default:
                errors.Add($"'layout' must be one of: {string.Join(", ", TheoryCardLayouts)} (got '{layout}').");
                break;
        }
    }


    private static void ValidateOptionsArray(JsonElement optionsElement, List<string> errors)
    {
        var correctCount = 0;
        var index = 0;
        foreach (var option in optionsElement.EnumerateArray())
        {
            if (option.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"options[{index}] must be an object.");
                index++;
                continue;
            }
            if (!option.TryGetProperty(ExerciseContentFields.Text, out var text) || text.ValueKind != JsonValueKind.String)
                errors.Add($"options[{index}].text must be a string.");
            if (!option.TryGetProperty(ExerciseContentFields.IsCorrect, out var isCorrect) || isCorrect.ValueKind != JsonValueKind.True && isCorrect.ValueKind != JsonValueKind.False)
                errors.Add($"options[{index}].is_correct must be a boolean.");
            else if (isCorrect.GetBoolean())
                correctCount++;
            index++;
        }

        if (correctCount == 0)
            errors.Add("options must have exactly one item with is_correct: true (found 0).");
        else if (correctCount > 1)
            errors.Add($"options must have exactly one item with is_correct: true (found {correctCount}).");
    }

    private static void RequireNonEmptyString(JsonElement root, string propertyName, List<string> errors)
    {
        if (!root.TryGetProperty(propertyName, out var propertyValue) || propertyValue.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(propertyValue.GetString()))
            errors.Add($"'{propertyName}' is required and must be a non-empty string.");
    }

    /// <summary>
    /// Unlike <see cref="RequireNonEmptyString"/> this accepts <c>""</c>. Used for the text either side
    /// of a fill-in-the-blank, where an empty side is legitimate — the blank can start or end the
    /// sentence.
    /// </summary>
    private static void RequireString(JsonElement root, string propertyName, List<string> errors)
    {
        if (!root.TryGetProperty(propertyName, out var propertyValue) || propertyValue.ValueKind != JsonValueKind.String)
            errors.Add($"'{propertyName}' is required and must be a string.");
    }

    /// <summary>
    /// Returns <see langword="null"/> — having already recorded the error — when the property is
    /// missing or not an array, which is the caller's signal to skip walking into its items rather
    /// than pile on errors about a structure that is not there.
    /// </summary>
    private static JsonElement? RequireArray(JsonElement root, string propertyName, List<string> errors)
    {
        if (!root.TryGetProperty(propertyName, out var propertyValue) || propertyValue.ValueKind != JsonValueKind.Array)
        {
            errors.Add($"'{propertyName}' is required and must be an array.");
            return null;
        }
        return propertyValue;
    }
}
