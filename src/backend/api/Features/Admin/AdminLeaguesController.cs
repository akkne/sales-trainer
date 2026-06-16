using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Gamification.Models;
using SalesTrainer.Api.Features.League.Models;
using SalesTrainer.Api.Features.League.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Admin;

[ApiController]
[Route("admin/leagues")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminLeaguesController(
    AppDbContext database,
    ILeagueService leagueService,
    ILogger<AdminLeaguesController> logger) : ControllerBase
{
    private const string XpCorrectionSource = "admin_correction";

    private Task<List<string>> LoadTierKeysAsync() =>
        database.LeagueTiers.OrderBy(t => t.Order).Select(t => t.Key).ToListAsync();

    [HttpGet]
    public async Task<ActionResult<List<AdminLeagueListItemDto>>> GetAll(
        [FromQuery] DateOnly? weekStart, [FromQuery] string? tier)
    {
        var tierKeys = await LoadTierKeysAsync();
        if (tier is not null && !tierKeys.Contains(tier))
            return BadRequest(new { message = $"Unknown tier: {tier}" });

        var query = database.Leagues.AsQueryable();
        if (weekStart.HasValue)
            query = query.Where(l => l.WeekStartDate == weekStart.Value);
        if (tier is not null)
            query = query.Where(l => l.Tier == tier);

        var leagues = await query
            .OrderByDescending(l => l.WeekStartDate)
            .Select(l => new AdminLeagueListItemDto(
                l.Id, l.Tier, l.WeekStartDate, l.WeekEndDate,
                // Count only memberships whose user still exists, so the list count
                // matches the member list on the detail page (which inner-joins Users).
                // An orphaned membership (user deleted directly in the DB) is otherwise
                // counted here but not shown there.
                database.LeagueMemberships.Count(m => m.LeagueId == l.Id
                    && database.Users.Any(u => u.Id == m.UserId))))
            .ToListAsync();

        var ordered = leagues
            .OrderByDescending(l => l.WeekStartDate)
            .ThenByDescending(l => tierKeys.IndexOf(l.Tier))
            .ToList();

        return Ok(ordered);
    }

    [HttpGet("weeks")]
    public async Task<ActionResult<List<DateOnly>>> GetWeeks()
    {
        var weeks = await database.Leagues
            .Select(l => l.WeekStartDate)
            .Distinct()
            .OrderByDescending(weekStart => weekStart)
            .ToListAsync();

        return Ok(weeks);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminLeagueDetailDto>> GetById(Guid id)
    {
        var detail = await BuildDetailAsync(id);
        if (detail is null) return NotFound();
        return Ok(detail);
    }

    [HttpPost("close-current")]
    public async Task<IActionResult> CloseCurrentWeek()
    {
        await leagueService.CloseCurrentLeagueAndCreateNextAsync();

        logger.LogWarning("League week manually closed by ActorId={ActorId}",
            User.FindFirstValue(ClaimTypes.NameIdentifier));

        return NoContent();
    }

    [HttpPost("{id:guid}/resync")]
    public async Task<ActionResult<AdminLeagueDetailDto>> Resync(Guid id)
    {
        var leagueExists = await database.Leagues.AnyAsync(l => l.Id == id);
        if (!leagueExists) return NotFound();

        await leagueService.SyncLeagueWeeklyXpAsync(id);

        logger.LogInformation("League XP resynced LeagueId={LeagueId} by ActorId={ActorId}",
            id, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(await BuildDetailAsync(id));
    }

    [HttpPut("memberships/{membershipId:guid}/tier")]
    public async Task<ActionResult<AdminLeagueDetailDto>> MoveMembershipTier(
        Guid membershipId, [FromBody] MoveMembershipTierRequestDto request)
    {
        var tierKeys = await LoadTierKeysAsync();
        if (!tierKeys.Contains(request.Tier))
            return BadRequest(new { message = $"Unknown tier: {request.Tier}" });

        var membership = await database.LeagueMemberships
            .FirstOrDefaultAsync(m => m.Id == membershipId);
        if (membership is null) return NotFound();

        var currentLeague = await database.Leagues
            .FirstOrDefaultAsync(l => l.Id == membership.LeagueId);
        if (currentLeague is null) return NotFound();

        if (currentLeague.Tier == request.Tier)
            return Ok(await BuildDetailAsync(currentLeague.Id));

        var targetLeague = await database.Leagues
            .FirstOrDefaultAsync(l => l.WeekStartDate == currentLeague.WeekStartDate && l.Tier == request.Tier);

        if (targetLeague is null)
        {
            targetLeague = new League.Models.League
            {
                Id = Guid.NewGuid(),
                Tier = request.Tier,
                WeekStartDate = currentLeague.WeekStartDate,
                WeekEndDate = currentLeague.WeekEndDate
            };
            database.Leagues.Add(targetLeague);
        }

        membership.LeagueId = targetLeague.Id;
        membership.Rank = 0;
        // The promotion outcome belonged to the previous tier; clear it so a stale
        // promoted/demoted badge does not follow the member into the new league.
        membership.PromotionOutcome = null;
        await database.SaveChangesAsync();

        // Recompute weekly XP for the league the member just joined so their XP and
        // the relative ranking of everyone in that league reflect the move.
        await leagueService.SyncLeagueWeeklyXpAsync(targetLeague.Id);

        logger.LogInformation(
            "League membership moved MembershipId={MembershipId} UserId={UserId} {OldTier} → {NewTier} by ActorId={ActorId}",
            membership.Id, membership.UserId, currentLeague.Tier, request.Tier,
            User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(await BuildDetailAsync(targetLeague.Id));
    }

    [HttpPut("memberships/{membershipId:guid}/xp")]
    public async Task<ActionResult<AdminLeagueDetailDto>> AdjustMembershipXp(
        Guid membershipId, [FromBody] AdjustMembershipXpRequestDto request)
    {
        if (request.Delta == 0)
            return BadRequest(new { message = "Delta must be non-zero" });

        var membership = await database.LeagueMemberships
            .FirstOrDefaultAsync(m => m.Id == membershipId);
        if (membership is null) return NotFound();

        var league = await database.Leagues
            .FirstOrDefaultAsync(l => l.Id == membership.LeagueId);
        if (league is null) return NotFound();

        // A direct WeeklyXpAmount write would be overwritten by the next XP sync,
        // so the adjustment is recorded as a UserXp correction record instead.
        // EarnedAt is stamped at the league's week start so the correction counts
        // for that week only and does not leak into future weeks.
        database.UserXpRecords.Add(new UserXp
        {
            Id = Guid.NewGuid(),
            UserId = membership.UserId,
            Amount = request.Delta,
            Source = XpCorrectionSource,
            EarnedAt = league.WeekStartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
        });
        await database.SaveChangesAsync();

        await leagueService.SyncLeagueWeeklyXpAsync(league.Id);

        logger.LogInformation(
            "League XP adjusted MembershipId={MembershipId} UserId={UserId} Delta={Delta} by ActorId={ActorId}",
            membership.Id, membership.UserId, request.Delta,
            User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(await BuildDetailAsync(league.Id));
    }

    [HttpDelete("memberships/{membershipId:guid}")]
    public async Task<IActionResult> RemoveMembership(Guid membershipId)
    {
        var membership = await database.LeagueMemberships
            .FirstOrDefaultAsync(m => m.Id == membershipId);
        if (membership is null) return NotFound();

        database.LeagueMemberships.Remove(membership);
        await database.SaveChangesAsync();

        logger.LogWarning(
            "League membership removed MembershipId={MembershipId} UserId={UserId} LeagueId={LeagueId} by ActorId={ActorId}",
            membership.Id, membership.UserId, membership.LeagueId,
            User.FindFirstValue(ClaimTypes.NameIdentifier));

        return NoContent();
    }

    [HttpGet("settings")]
    public async Task<ActionResult<LeagueSettingsDto>> GetSettings()
    {
        var settings = await leagueService.GetSettingsAsync();
        return Ok(ToSettingsDto(settings));
    }

    [HttpPut("settings")]
    public async Task<ActionResult<LeagueSettingsDto>> UpdateSettings(
        [FromBody] UpdateLeagueSettingsRequestDto request)
    {
        if (request.MaximumLeagueParticipantCount <= 0 ||
            request.PromotionZoneSize <= 0 ||
            request.DemotionZoneSize <= 0)
            return BadRequest(new { message = "All settings values must be positive" });

        if (request.PromotionZoneSize + request.DemotionZoneSize > request.MaximumLeagueParticipantCount)
            return BadRequest(new { message = "Promotion + demotion zones cannot exceed maximum participant count" });

        if (request.PeriodLengthDays is <= 0)
            return BadRequest(new { message = "Period length must be positive" });

        // Ensures the row exists and the period is initialized before we edit it.
        var settings = await leagueService.GetSettingsAsync();

        settings.MaximumLeagueParticipantCount = request.MaximumLeagueParticipantCount;
        settings.PromotionZoneSize = request.PromotionZoneSize;
        settings.DemotionZoneSize = request.DemotionZoneSize;

        if (request.PeriodLengthDays is { } periodLength)
            settings.PeriodLengthDays = periodLength;

        if (request.CurrentPeriodEndsAt is { } endsAt)
        {
            settings.CurrentPeriodEndsAt = endsAt;
            // Keep the active period's leagues aligned with the new end date so the
            // XP window (WeekStartDate..WeekEndDate) extends/shrinks with the schedule.
            var newEndDate = DateOnly.FromDateTime(endsAt.UtcDateTime);
            var activeLeagues = await database.Leagues
                .Where(l => l.WeekStartDate == settings.CurrentPeriodStartDate)
                .ToListAsync();
            foreach (var league in activeLeagues)
                league.WeekEndDate = newEndDate;
        }

        await database.SaveChangesAsync();

        logger.LogInformation(
            "League settings updated Max={Max} Promotion={Promotion} Demotion={Demotion} PeriodEndsAt={PeriodEndsAt} PeriodLength={PeriodLength} by ActorId={ActorId}",
            settings.MaximumLeagueParticipantCount, settings.PromotionZoneSize, settings.DemotionZoneSize,
            settings.CurrentPeriodEndsAt, settings.PeriodLengthDays,
            User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(ToSettingsDto(settings));
    }

    private static LeagueSettingsDto ToSettingsDto(LeagueSettings settings) =>
        new(settings.MaximumLeagueParticipantCount,
            settings.PromotionZoneSize,
            settings.DemotionZoneSize,
            settings.CurrentPeriodEndsAt,
            settings.PeriodLengthDays);

    [HttpGet("tiers")]
    public async Task<ActionResult<List<AdminLeagueTierDto>>> GetTiers()
    {
        var tiers = await database.LeagueTiers
            .OrderBy(t => t.Order)
            .Select(t => new AdminLeagueTierDto(t.Id, t.Key, t.Name, t.Color, t.Order))
            .ToListAsync();
        return Ok(tiers);
    }

    [HttpPost("tiers")]
    public async Task<ActionResult<AdminLeagueTierDto>> CreateTier(
        [FromBody] CreateLeagueTierRequestDto request)
    {
        var key = (request.Key ?? string.Empty).Trim().ToLowerInvariant();
        var validation = ValidateTierFields(key, request.Name, request.Color);
        if (validation is not null) return BadRequest(new { message = validation });

        if (await database.LeagueTiers.AnyAsync(t => t.Key == key))
            return BadRequest(new { message = $"Tier with key '{key}' already exists" });

        var tier = new LeagueTier
        {
            Id = Guid.NewGuid(),
            Key = key,
            Name = request.Name.Trim(),
            Color = request.Color.Trim(),
            Order = request.Order
        };
        database.LeagueTiers.Add(tier);
        await database.SaveChangesAsync();

        logger.LogInformation("League tier created Key={Key} by ActorId={ActorId}",
            tier.Key, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminLeagueTierDto(tier.Id, tier.Key, tier.Name, tier.Color, tier.Order));
    }

    [HttpPut("tiers/{id:guid}")]
    public async Task<ActionResult<AdminLeagueTierDto>> UpdateTier(
        Guid id, [FromBody] UpdateLeagueTierRequestDto request)
    {
        // The key (slug) is immutable: it is stored on every League row, so renaming
        // it would orphan history. Only label, color, and order are editable.
        var validation = ValidateTierFields(key: "x", request.Name, request.Color);
        if (validation is not null) return BadRequest(new { message = validation });

        var tier = await database.LeagueTiers.FirstOrDefaultAsync(t => t.Id == id);
        if (tier is null) return NotFound();

        tier.Name = request.Name.Trim();
        tier.Color = request.Color.Trim();
        tier.Order = request.Order;
        await database.SaveChangesAsync();

        logger.LogInformation("League tier updated Key={Key} by ActorId={ActorId}",
            tier.Key, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new AdminLeagueTierDto(tier.Id, tier.Key, tier.Name, tier.Color, tier.Order));
    }

    [HttpDelete("tiers/{id:guid}")]
    public async Task<IActionResult> DeleteTier(Guid id)
    {
        var tier = await database.LeagueTiers.FirstOrDefaultAsync(t => t.Id == id);
        if (tier is null) return NotFound();

        if (await database.LeagueTiers.CountAsync() <= 1)
            return BadRequest(new { message = "At least one tier must remain" });

        if (await database.Leagues.AnyAsync(l => l.Tier == tier.Key))
            return BadRequest(new { message = "Cannot delete a tier that has existing leagues; reassign members first" });

        database.LeagueTiers.Remove(tier);
        await database.SaveChangesAsync();

        logger.LogWarning("League tier deleted Key={Key} by ActorId={ActorId}",
            tier.Key, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return NoContent();
    }

    private static string? ValidateTierFields(string key, string? name, string? color)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "Key is required";
        if (string.IsNullOrWhiteSpace(name))
            return "Name is required";
        if (string.IsNullOrWhiteSpace(color))
            return "Color is required";
        return null;
    }

    private async Task<AdminLeagueDetailDto?> BuildDetailAsync(Guid leagueId)
    {
        var league = await database.Leagues.FirstOrDefaultAsync(l => l.Id == leagueId);
        if (league is null) return null;

        var members = await database.LeagueMemberships
            .Where(m => m.LeagueId == leagueId)
            .Join(
                database.Users,
                membership => membership.UserId,
                user => user.Id,
                (membership, user) => new { membership, user })
            .OrderByDescending(pair => pair.membership.WeeklyXpAmount)
            .Select(pair => new AdminLeagueMemberDto(
                pair.membership.Id,
                pair.user.Id,
                pair.user.DisplayName,
                pair.user.Email,
                pair.membership.WeeklyXpAmount,
                pair.membership.Rank,
                pair.membership.PromotionOutcome))
            .ToListAsync();

        return new AdminLeagueDetailDto(
            league.Id, league.Tier, league.WeekStartDate, league.WeekEndDate, members);
    }
}
