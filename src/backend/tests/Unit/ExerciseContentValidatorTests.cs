using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;
using SalesTrainer.Api.Features.Exercises.Services;

namespace SalesTrainer.Tests.Unit;

[TestFixture]
public class ExerciseContentValidatorTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static JsonElement Json(object obj) =>
        JsonDocument.Parse(JsonSerializer.Serialize(obj)).RootElement;

    private static IReadOnlyList<string> Validate(string type, object content) =>
        ExerciseContentValidator.Validate(type, Json(content));

    // ── unknown type ─────────────────────────────────────────────────────────

    [Test]
    public void UnknownType_ReturnsError()
    {
        var errors = Validate("totally_fake", new { });
        errors.Should().ContainSingle(e => e.Contains("Unknown exercise type 'totally_fake'"));
    }

    // ── content not an object ────────────────────────────────────────────────

    [Test]
    public void ContentNotObject_ReturnsError()
    {
        var raw = JsonDocument.Parse("\"not an object\"").RootElement;
        var errors = ExerciseContentValidator.Validate("choose_option", raw);
        errors.Should().ContainSingle(e => e.Contains("content must be a JSON object"));
    }

    // ── choose_option ────────────────────────────────────────────────────────

    [Test]
    public void ChooseOption_Valid_NoErrors()
    {
        var errors = Validate("choose_option", new
        {
            situation = "Test situation",
            options = new[]
            {
                new { text = "A", is_correct = false },
                new { text = "B", is_correct = true }
            }
        });
        errors.Should().BeEmpty();
    }

    [Test]
    public void ChooseOption_ZeroCorrectOptions_ReturnsError()
    {
        var errors = Validate("choose_option", new
        {
            situation = "Test situation",
            options = new[]
            {
                new { text = "A", is_correct = false },
                new { text = "B", is_correct = false }
            }
        });
        errors.Should().ContainSingle(e => e.Contains("exactly one item with is_correct: true (found 0)"));
    }

    [Test]
    public void ChooseOption_TwoCorrectOptions_ReturnsError()
    {
        var errors = Validate("choose_option", new
        {
            situation = "Test situation",
            options = new[]
            {
                new { text = "A", is_correct = true },
                new { text = "B", is_correct = true }
            }
        });
        errors.Should().ContainSingle(e => e.Contains("exactly one item with is_correct: true (found 2)"));
    }

    [Test]
    public void ChooseOption_OnlyOneOption_ReturnsError()
    {
        var errors = Validate("choose_option", new
        {
            situation = "Test situation",
            options = new[]
            {
                new { text = "A", is_correct = true }
            }
        });
        errors.Should().Contain(e => e.Contains("at least 2 items"));
    }

    [Test]
    public void ChooseOption_MissingSituation_ReturnsError()
    {
        var errors = Validate("choose_option", new
        {
            options = new[]
            {
                new { text = "A", is_correct = false },
                new { text = "B", is_correct = true }
            }
        });
        errors.Should().Contain(e => e.Contains("'situation'"));
    }

    // ── fill_blank ───────────────────────────────────────────────────────────

    [Test]
    public void FillBlank_Valid_NoErrors()
    {
        var errors = Validate("fill_blank", new
        {
            before = "Hello",
            after = "world",
            options = new[]
            {
                new { text = "A", is_correct = true },
                new { text = "B", is_correct = false }
            }
        });
        errors.Should().BeEmpty();
    }

    [Test]
    public void FillBlank_TwoCorrectOptions_ReturnsError()
    {
        var errors = Validate("fill_blank", new
        {
            before = "Hello",
            after = "world",
            options = new[]
            {
                new { text = "A", is_correct = true },
                new { text = "B", is_correct = true }
            }
        });
        errors.Should().ContainSingle(e => e.Contains("exactly one item with is_correct: true (found 2)"));
    }

    // ── reorder ──────────────────────────────────────────────────────────────

    [Test]
    public void Reorder_Valid_NoErrors()
    {
        var errors = Validate("reorder", new
        {
            instruction = "Put in order",
            items = new[]
            {
                new { text = "First", correct_position = 1 },
                new { text = "Second", correct_position = 2 }
            }
        });
        errors.Should().BeEmpty();
    }

    [Test]
    public void Reorder_DuplicateCorrectPosition_ReturnsError()
    {
        var errors = Validate("reorder", new
        {
            instruction = "Put in order",
            items = new[]
            {
                new { text = "First", correct_position = 1 },
                new { text = "Second", correct_position = 1 }
            }
        });
        errors.Should().ContainSingle(e => e.Contains("correct_position values in items must be unique"));
    }

    [Test]
    public void Reorder_OnlyOneItem_ReturnsError()
    {
        var errors = Validate("reorder", new
        {
            instruction = "Put in order",
            items = new[]
            {
                new { text = "Only", correct_position = 1 }
            }
        });
        errors.Should().Contain(e => e.Contains("at least 2 items"));
    }

    // ── match_pairs ──────────────────────────────────────────────────────────

    [Test]
    public void MatchPairs_Valid_NoErrors()
    {
        var errors = Validate("match_pairs", new
        {
            instruction = "Match these",
            pairs = new[]
            {
                new { left = "A", right = "1" },
                new { left = "B", right = "2" }
            }
        });
        errors.Should().BeEmpty();
    }

    [Test]
    public void MatchPairs_OnlyOnePair_ReturnsError()
    {
        var errors = Validate("match_pairs", new
        {
            instruction = "Match these",
            pairs = new[]
            {
                new { left = "A", right = "1" }
            }
        });
        errors.Should().Contain(e => e.Contains("at least 2 items"));
    }

    // ── categorize ───────────────────────────────────────────────────────────

    [Test]
    public void Categorize_Valid_NoErrors()
    {
        var errors = Validate("categorize", new
        {
            instruction = "Sort these",
            categories = new[] { "Fruit", "Vegetable" },
            items = new[]
            {
                new { text = "Apple", category = "Fruit" },
                new { text = "Carrot", category = "Vegetable" }
            }
        });
        errors.Should().BeEmpty();
    }

    [Test]
    public void Categorize_ItemCategoryNotInCategories_ReturnsError()
    {
        var errors = Validate("categorize", new
        {
            instruction = "Sort these",
            categories = new[] { "Fruit", "Vegetable" },
            items = new[]
            {
                new { text = "Chicken", category = "Meat" }
            }
        });
        errors.Should().Contain(e => e.Contains("'Meat' is not one of the declared categories"));
    }

    [Test]
    public void Categorize_OnlyOneCategory_ReturnsError()
    {
        var errors = Validate("categorize", new
        {
            instruction = "Sort these",
            categories = new[] { "Fruit" },
            items = new[]
            {
                new { text = "Apple", category = "Fruit" }
            }
        });
        errors.Should().Contain(e => e.Contains("categories must contain at least 2 items"));
    }

    // ── spot_mistake ─────────────────────────────────────────────────────────

    [Test]
    public void SpotMistake_Valid_NoErrors()
    {
        var errors = Validate("spot_mistake", new
        {
            dialogue = new[]
            {
                new { speaker = "A", text = "Hello", is_mistake = false },
                new { speaker = "B", text = "Goodbye bad", is_mistake = true }
            }
        });
        errors.Should().BeEmpty();
    }

    [Test]
    public void SpotMistake_ZeroMistakes_ReturnsError()
    {
        var errors = Validate("spot_mistake", new
        {
            dialogue = new[]
            {
                new { speaker = "A", text = "Hello", is_mistake = false },
                new { speaker = "B", text = "World", is_mistake = false }
            }
        });
        errors.Should().ContainSingle(e => e.Contains("exactly one item with is_mistake: true (found 0)"));
    }

    [Test]
    public void SpotMistake_TwoMistakes_ReturnsError()
    {
        var errors = Validate("spot_mistake", new
        {
            dialogue = new[]
            {
                new { speaker = "A", text = "Hello", is_mistake = true },
                new { speaker = "B", text = "World", is_mistake = true }
            }
        });
        errors.Should().ContainSingle(e => e.Contains("exactly one item with is_mistake: true (found 2)"));
    }

    // ── rewrite ───────────────────────────────────────────────────────────────

    [Test]
    public void Rewrite_Valid_NoErrors()
    {
        var errors = Validate("rewrite", new
        {
            instruction = "Rewrite this",
            original = "Bad sentence here."
        });
        errors.Should().BeEmpty();
    }

    [Test]
    public void Rewrite_MissingInstruction_ReturnsError()
    {
        var errors = Validate("rewrite", new
        {
            original = "Bad sentence here."
        });
        errors.Should().Contain(e => e.Contains("'instruction'"));
    }

    // ── ai_dialogue ──────────────────────────────────────────────────────────

    [Test]
    public void AiDialogue_Valid_NoErrors()
    {
        var errors = Validate("ai_dialogue", new
        {
            persona = "Sales rep",
            scenario = "Cold call"
        });
        errors.Should().BeEmpty();
    }

    [Test]
    public void AiDialogue_MaxTurnsZero_ReturnsError()
    {
        var errors = Validate("ai_dialogue", new
        {
            persona = "Sales rep",
            scenario = "Cold call",
            max_turns = 0
        });
        errors.Should().Contain(e => e.Contains("max_turns must be at least 1"));
    }

    // ── evaluate_call ─────────────────────────────────────────────────────────

    [Test]
    public void EvaluateCall_Valid_NoErrors()
    {
        var errors = Validate("evaluate_call", new
        {
            transcript = new[]
            {
                new { speaker = "Agent", text = "Hello" }
            },
            evaluation_axes = new[]
            {
                new { name = "Tone", description = "Was the tone good?" }
            }
        });
        errors.Should().BeEmpty();
    }

    [Test]
    public void EvaluateCall_EmptyEvaluationAxes_ReturnsError()
    {
        var errors = Validate("evaluate_call", new
        {
            transcript = new[]
            {
                new { speaker = "Agent", text = "Hello" }
            },
            evaluation_axes = Array.Empty<object>()
        });
        errors.Should().Contain(e => e.Contains("evaluation_axes must contain at least 1 item"));
    }

    // ── free_text ─────────────────────────────────────────────────────────────

    [Test]
    public void FreeText_Valid_NoErrors()
    {
        var errors = Validate("free_text", new
        {
            instruction = "Write something"
        });
        errors.Should().BeEmpty();
    }

    [Test]
    public void FreeText_MissingInstruction_ReturnsError()
    {
        var errors = Validate("free_text", new
        {
            situation = "A situation"
        });
        errors.Should().Contain(e => e.Contains("'instruction'"));
    }
}
