using System.Text.Json;

namespace SalesTrainer.Api.Features.Exercises.Services;

/// <summary>
/// Validates the <c>content</c> JSON element of an exercise against the canonical per-type schema.
/// Returns human-readable English error strings; an empty list means the content is valid.
/// </summary>
public static class ExerciseContentValidator
{
    /// <summary>
    /// Validates <paramref name="content"/> for the given exercise <paramref name="type"/>.
    /// </summary>
    /// <returns>List of error messages. Empty == valid.</returns>
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

    // ── choose_option ────────────────────────────────────────────────────────
    // { situation:string(req), options:[{text,is_correct}] (>=2, exactly one true), explanation? }
    private static void ValidateChooseOption(JsonElement root, List<string> errors)
    {
        RequireNonEmptyString(root, "situation", errors);
        var options = RequireArray(root, "options", errors);
        if (options is not null)
        {
            if (options.Value.GetArrayLength() < 2)
                errors.Add("options must contain at least 2 items.");
            ValidateOptionsArray(options.Value, errors);
        }
    }

    // ── fill_blank ───────────────────────────────────────────────────────────
    // { before:string, after:string, options:[{text,is_correct}] (>=2, exactly one true), explanation? }
    private static void ValidateFillBlank(JsonElement root, List<string> errors)
    {
        RequireString(root, "before", errors);
        RequireString(root, "after", errors);
        var options = RequireArray(root, "options", errors);
        if (options is not null)
        {
            if (options.Value.GetArrayLength() < 2)
                errors.Add("options must contain at least 2 items.");
            ValidateOptionsArray(options.Value, errors);
        }
    }

    // ── reorder ──────────────────────────────────────────────────────────────
    // { instruction:string(req), items:[{text,correct_position:int}] (>=2, correct_position unique), explanation? }
    private static void ValidateReorder(JsonElement root, List<string> errors)
    {
        RequireNonEmptyString(root, "instruction", errors);
        var items = RequireArray(root, "items", errors);
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
            if (!item.TryGetProperty("text", out var textProp) || textProp.ValueKind != JsonValueKind.String)
                errors.Add($"items[{index}].text must be a string.");
            if (!item.TryGetProperty("correct_position", out var posProp) || posProp.ValueKind != JsonValueKind.Number)
                errors.Add($"items[{index}].correct_position must be an integer.");
            else
                positions.Add(posProp.GetInt32());
            index++;
        }

        if (positions.Count != positions.Distinct().Count())
            errors.Add("correct_position values in items must be unique.");
    }

    // ── match_pairs ──────────────────────────────────────────────────────────
    // { instruction:string(req), pairs:[{left,right}] (>=2), explanation? }
    private static void ValidateMatchPairs(JsonElement root, List<string> errors)
    {
        RequireNonEmptyString(root, "instruction", errors);
        var pairs = RequireArray(root, "pairs", errors);
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
            if (!pair.TryGetProperty("left", out var left) || left.ValueKind != JsonValueKind.String)
                errors.Add($"pairs[{index}].left must be a string.");
            if (!pair.TryGetProperty("right", out var right) || right.ValueKind != JsonValueKind.String)
                errors.Add($"pairs[{index}].right must be a string.");
            index++;
        }
    }

    // ── categorize ───────────────────────────────────────────────────────────
    // { instruction:string(req), categories:[string] (>=2 non-empty), items:[{text,category}] (>=1, each category in categories), explanation? }
    private static void ValidateCategorize(JsonElement root, List<string> errors)
    {
        RequireNonEmptyString(root, "instruction", errors);

        var categoriesEl = RequireArray(root, "categories", errors);
        var categories = new HashSet<string>(StringComparer.Ordinal);
        if (categoriesEl is not null)
        {
            if (categoriesEl.Value.GetArrayLength() < 2)
                errors.Add("categories must contain at least 2 items.");

            var catIndex = 0;
            foreach (var cat in categoriesEl.Value.EnumerateArray())
            {
                if (cat.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(cat.GetString()))
                    errors.Add($"categories[{catIndex}] must be a non-empty string.");
                else
                    categories.Add(cat.GetString()!);
                catIndex++;
            }
        }

        var itemsEl = RequireArray(root, "items", errors);
        if (itemsEl is null) return;

        if (itemsEl.Value.GetArrayLength() < 1)
        {
            errors.Add("items must contain at least 1 item.");
            return;
        }

        var index = 0;
        foreach (var item in itemsEl.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"items[{index}] must be an object.");
                index++;
                continue;
            }
            if (!item.TryGetProperty("text", out var textProp) || textProp.ValueKind != JsonValueKind.String)
                errors.Add($"items[{index}].text must be a string.");
            if (!item.TryGetProperty("category", out var catProp) || catProp.ValueKind != JsonValueKind.String)
                errors.Add($"items[{index}].category must be a string.");
            else if (categories.Count > 0 && !categories.Contains(catProp.GetString()!))
                errors.Add($"items[{index}].category '{catProp.GetString()}' is not one of the declared categories.");
            index++;
        }
    }

    // ── spot_mistake ─────────────────────────────────────────────────────────
    // { dialogue:[{speaker,text,is_mistake:bool}] (>=2, exactly one true), explanation?, ai_prompt? }
    private static void ValidateSpotMistake(JsonElement root, List<string> errors)
    {
        var dialogue = RequireArray(root, "dialogue", errors);
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
            if (!line.TryGetProperty("speaker", out var speaker) || speaker.ValueKind != JsonValueKind.String)
                errors.Add($"dialogue[{index}].speaker must be a string.");
            if (!line.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String)
                errors.Add($"dialogue[{index}].text must be a string.");
            if (!line.TryGetProperty("is_mistake", out var isMistake) || isMistake.ValueKind != JsonValueKind.True && isMistake.ValueKind != JsonValueKind.False)
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

    // ── rewrite ───────────────────────────────────────────────────────────────
    // { instruction:string(req), original:string(req), evaluation_criteria?:[string], ai_prompt? }
    private static void ValidateRewrite(JsonElement root, List<string> errors)
    {
        RequireNonEmptyString(root, "instruction", errors);
        RequireNonEmptyString(root, "original", errors);
    }

    // ── ai_dialogue ──────────────────────────────────────────────────────────
    // { persona:string(req), scenario:string(req), context?, max_turns?:int(>=1), success_criteria?:[string], ai_prompt? }
    private static void ValidateAiDialogue(JsonElement root, List<string> errors)
    {
        RequireNonEmptyString(root, "persona", errors);
        RequireNonEmptyString(root, "scenario", errors);

        if (root.TryGetProperty("max_turns", out var maxTurns))
        {
            if (maxTurns.ValueKind != JsonValueKind.Number)
                errors.Add("max_turns must be an integer.");
            else if (maxTurns.GetInt32() < 1)
                errors.Add("max_turns must be at least 1.");
        }
    }

    // ── evaluate_call ────────────────────────────────────────────────────────
    // { transcript:[{speaker,text}] (>=1), evaluation_axes:[{name,description}] (>=1), ai_prompt? }
    private static void ValidateEvaluateCall(JsonElement root, List<string> errors)
    {
        var transcript = RequireArray(root, "transcript", errors);
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
                    if (!line.TryGetProperty("speaker", out var speaker) || speaker.ValueKind != JsonValueKind.String)
                        errors.Add($"transcript[{index}].speaker must be a string.");
                    if (!line.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String)
                        errors.Add($"transcript[{index}].text must be a string.");
                    index++;
                }
            }
        }

        var axes = RequireArray(root, "evaluation_axes", errors);
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
                    if (!axis.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String)
                        errors.Add($"evaluation_axes[{index}].name must be a string.");
                    if (!axis.TryGetProperty("description", out var desc) || desc.ValueKind != JsonValueKind.String)
                        errors.Add($"evaluation_axes[{index}].description must be a string.");
                    index++;
                }
            }
        }
    }

    // ── free_text ─────────────────────────────────────────────────────────────
    // { situation?, instruction:string(req), evaluation_criteria?:[string], ai_prompt? }
    private static void ValidateFreeText(JsonElement root, List<string> errors)
    {
        RequireNonEmptyString(root, "instruction", errors);
    }

    // ── theory_card ──────────────────────────────────────────────────────────
    // A non-graded "story" card. Shape depends on `layout`:
    //   layout:"text"     { layout, title?, body:string(req) }
    //   layout:"dialogue" { layout, title?, turns:[{side:"me"|"them", text:string, annotations?:[string]}] (>=1) }
    //   layout:"bullets"  { layout, title?, items:[string] (>=1 non-empty) }
    //   layout:"quote"    { layout, text:string(req), author? }
    private static readonly string[] TheoryCardLayouts = ["text", "dialogue", "bullets", "quote"];

    private static void ValidateTheoryCard(JsonElement root, List<string> errors)
    {
        if (!root.TryGetProperty("layout", out var layoutEl) || layoutEl.ValueKind != JsonValueKind.String)
        {
            errors.Add($"'layout' is required and must be one of: {string.Join(", ", TheoryCardLayouts)}.");
            return;
        }

        var layout = layoutEl.GetString();
        switch (layout)
        {
            case "text":
                RequireNonEmptyString(root, "body", errors);
                break;

            case "dialogue":
            {
                var turns = RequireArray(root, "turns", errors);
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
                    if (!turn.TryGetProperty("side", out var side) || side.ValueKind != JsonValueKind.String
                        || (side.GetString() != "me" && side.GetString() != "them"))
                        errors.Add($"turns[{index}].side must be \"me\" or \"them\".");
                    if (!turn.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String
                        || string.IsNullOrWhiteSpace(text.GetString()))
                        errors.Add($"turns[{index}].text must be a non-empty string.");
                    index++;
                }
                break;
            }

            case "bullets":
            {
                var items = RequireArray(root, "items", errors);
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

            case "quote":
                RequireNonEmptyString(root, "text", errors);
                break;

            default:
                errors.Add($"'layout' must be one of: {string.Join(", ", TheoryCardLayouts)} (got '{layout}').");
                break;
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    /// <summary>Validates the shared {text:string, is_correct:bool} options array used by choose_option and fill_blank.</summary>
    private static void ValidateOptionsArray(JsonElement optionsEl, List<string> errors)
    {
        var correctCount = 0;
        var index = 0;
        foreach (var option in optionsEl.EnumerateArray())
        {
            if (option.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"options[{index}] must be an object.");
                index++;
                continue;
            }
            if (!option.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String)
                errors.Add($"options[{index}].text must be a string.");
            if (!option.TryGetProperty("is_correct", out var isCorrect) || isCorrect.ValueKind != JsonValueKind.True && isCorrect.ValueKind != JsonValueKind.False)
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
        if (!root.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(prop.GetString()))
            errors.Add($"'{propertyName}' is required and must be a non-empty string.");
    }

    private static void RequireString(JsonElement root, string propertyName, List<string> errors)
    {
        if (!root.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.String)
            errors.Add($"'{propertyName}' is required and must be a string.");
    }

    private static JsonElement? RequireArray(JsonElement root, string propertyName, List<string> errors)
    {
        if (!root.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.Array)
        {
            errors.Add($"'{propertyName}' is required and must be an array.");
            return null;
        }
        return prop;
    }
}
