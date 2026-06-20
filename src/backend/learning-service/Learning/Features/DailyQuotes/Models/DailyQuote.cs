namespace Sellevate.Learning.Features.DailyQuotes.Models;

public sealed class DailyQuote
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public string Text { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
