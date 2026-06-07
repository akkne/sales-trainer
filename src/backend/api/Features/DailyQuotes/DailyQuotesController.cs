using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.DailyQuotes;

public sealed record DailyQuoteDto(string Text, string Author, DateOnly Date);

[ApiController]
[Authorize]
public sealed class DailyQuotesController(AppDbContext databaseContext) : ControllerBase
{
    /// <summary>
    /// Returns the quote scheduled for the given date (client's local date).
    /// Falls back to the most recent quote at or before that date so the
    /// widget keeps showing something when a day has no dedicated quote.
    /// </summary>
    [HttpGet("daily-quote")]
    public async Task<ActionResult<DailyQuoteDto>> GetForDate(
        [FromQuery] DateOnly? date,
        CancellationToken cancellationToken)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var quote = await databaseContext.DailyQuotes.AsNoTracking()
            .Where(candidate => candidate.Date <= targetDate)
            .OrderByDescending(candidate => candidate.Date)
            .FirstOrDefaultAsync(cancellationToken);

        if (quote is null) return NoContent();

        return Ok(new DailyQuoteDto(quote.Text, quote.Author, quote.Date));
    }
}
