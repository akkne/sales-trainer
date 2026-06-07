using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.DailyQuotes.Models;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Admin;

public sealed record AdminDailyQuoteDto(
    Guid Id,
    DateOnly Date,
    string Text,
    string Author,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record AdminDailyQuoteWriteRequestDto(
    DateOnly Date,
    string Text,
    string? Author);

[ApiController]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminDailyQuotesController(
    AppDbContext databaseContext,
    ILogger<AdminDailyQuotesController> logger) : ControllerBase
{
    [HttpGet("admin/daily-quotes")]
    public async Task<ActionResult<List<AdminDailyQuoteDto>>> GetAll(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var query = databaseContext.DailyQuotes.AsNoTracking().AsQueryable();

        if (from.HasValue)
            query = query.Where(quote => quote.Date >= from.Value);
        if (to.HasValue)
            query = query.Where(quote => quote.Date <= to.Value);

        var quotes = await query
            .OrderBy(quote => quote.Date)
            .ToListAsync(cancellationToken);

        return Ok(quotes.Select(MapToDto).ToList());
    }

    [HttpPost("admin/daily-quotes")]
    public async Task<ActionResult<AdminDailyQuoteDto>> Create(
        [FromBody] AdminDailyQuoteWriteRequestDto payload,
        CancellationToken cancellationToken)
    {
        var validationError = await ValidatePayloadAsync(payload, existingQuoteId: null, cancellationToken);
        if (validationError is not null) return validationError;

        var quote = new DailyQuote
        {
            Id = Guid.NewGuid(),
            Date = payload.Date,
            Text = payload.Text.Trim(),
            Author = payload.Author?.Trim() ?? string.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        databaseContext.DailyQuotes.Add(quote);
        await databaseContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Daily quote created QuoteId={QuoteId} Date={Date} by ActorId={ActorId}",
            quote.Id, quote.Date, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(MapToDto(quote));
    }

    [HttpPut("admin/daily-quotes/{id:guid}")]
    public async Task<ActionResult<AdminDailyQuoteDto>> Update(
        Guid id,
        [FromBody] AdminDailyQuoteWriteRequestDto payload,
        CancellationToken cancellationToken)
    {
        var quote = await databaseContext.DailyQuotes
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (quote is null) return NotFound();

        var validationError = await ValidatePayloadAsync(payload, existingQuoteId: id, cancellationToken);
        if (validationError is not null) return validationError;

        quote.Date = payload.Date;
        quote.Text = payload.Text.Trim();
        quote.Author = payload.Author?.Trim() ?? string.Empty;
        quote.UpdatedAt = DateTime.UtcNow;

        await databaseContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Daily quote updated QuoteId={QuoteId} Date={Date} by ActorId={ActorId}",
            quote.Id, quote.Date, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(MapToDto(quote));
    }

    [HttpDelete("admin/daily-quotes/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var quote = await databaseContext.DailyQuotes
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (quote is null) return NotFound();

        databaseContext.DailyQuotes.Remove(quote);
        await databaseContext.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Daily quote deleted QuoteId={QuoteId} Date={Date} by ActorId={ActorId}",
            id, quote.Date, User.FindFirstValue(ClaimTypes.NameIdentifier));
        return NoContent();
    }

    private async Task<ActionResult?> ValidatePayloadAsync(
        AdminDailyQuoteWriteRequestDto payload,
        Guid? existingQuoteId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(payload.Text))
            return BadRequest(new { error = "Text is required." });

        var dateClashExists = await databaseContext.DailyQuotes.AnyAsync(
            candidate => candidate.Date == payload.Date
                         && (existingQuoteId == null || candidate.Id != existingQuoteId),
            cancellationToken);

        if (dateClashExists)
            return Conflict(new { error = "A quote for this date already exists." });

        return null;
    }

    private static AdminDailyQuoteDto MapToDto(DailyQuote quote)
    {
        return new AdminDailyQuoteDto(
            quote.Id,
            quote.Date,
            quote.Text,
            quote.Author,
            quote.CreatedAt,
            quote.UpdatedAt);
    }
}
