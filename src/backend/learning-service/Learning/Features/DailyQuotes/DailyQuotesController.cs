using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Features.DailyQuotes;

[ApiController]
[Authorize]
public sealed class DailyQuotesController(LearningDbContext databaseContext) : ControllerBase
{
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
