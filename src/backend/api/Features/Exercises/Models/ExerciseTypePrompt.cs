namespace SalesTrainer.Api.Features.Exercises.Models;

/// <summary>
/// Global system prompt for AI-powered exercise types.
/// Combined with per-exercise aiPrompt from SerializedContent.
/// </summary>
public class ExerciseTypePrompt
{
    public Guid Id { get; set; }

    /// <summary>
    /// Exercise type key (e.g., "find_error", "ai_dialog", "rate_call", etc.)
    /// </summary>
    public string ExerciseType { get; set; } = "";

    /// <summary>
    /// Base system prompt applied to all exercises of this type.
    /// </summary>
    public string SystemPrompt { get; set; } = "";

    public DateTime UpdatedAt { get; set; }
}
