using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sellevate.Gamification.Common.Constants;
using Sellevate.Gamification.Features.Admin.Models;
using Sellevate.Gamification.Features.Gamification.Models;
using Sellevate.Gamification.Features.League.Models;
using Sellevate.Gamification.Features.League.Services.Abstract;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Features.Admin;

[ApiController]
[Route(RouteConstants.AdminLeagues)]
[Authorize(Policy = AuthorizationPolicies.RequireAdministrator)]
public sealed class AdminLeaguesController(
    GamificationDbContext databaseContext,
    ILeagueService leagueService,
    ILogger<AdminLeaguesController> logger) : ControllerBase
{
    private Task<List<string>> LoadTierKeysAsync(CancellationToken cancellationToken) =>
        databaseContext.LeagueTiers.OrderBy(tier => tier.Order).Select(tier => tier.Key).ToListAsync(cancellationToken);

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminLeagueListItemDto>>> GetAll(
        [FromQuery] DateOnly? weekStart,
        [FromQuery] string? tier,
        CancellationToken cancellationToken = default)
    {
        var tierKeys = await LoadTierKeysAsync(cancellationToken);
        if (tier is not null && !tierKeys.Contains(tier))
        {
            return BadRequest(new { message = $"Unknown tier: {tier}" });
        }

        var query = databaseContext.Leagues.AsQueryable();
        if (weekStart.HasValue)
        {
            query = query.Where(league => league.WeekStartDate == weekStart.Value);
        }

        if (tier is not null)
        {
            query = query.Where(league => league.Tier == tier);
        }

        // GA6(b): single group-join instead of a correlated subquery per row (N+1).
        // Precompute a tier-order lookup with a stable fallback for unknown tiers.
        var tierOrderMap = tierKeys
            .Select((key, index) => (key, index))
            .ToDictionary(pair => pair.key, pair => pair.index);

        var leagueList = await query
            .OrderByDescending(league => league.WeekStartDate)
            .Select(league => new { league.Id, league.Tier, league.WeekStartDate, league.WeekEndDate })
            .ToListAsync(cancellationToken);

        var leagueIds = leagueList.Select(l => l.Id).ToList();

        // Load member counts in one query using GroupBy.
        var memberCountByLeagueId = await databaseContext.LeagueMemberships
            .Where(membership => leagueIds.Contains(membership.LeagueId)
                && databaseContext.UserReplicas.Any(user => user.UserId == membership.UserId))
            .GroupBy(membership => membership.LeagueId)
            .Select(group => new { LeagueId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.LeagueId, entry => entry.Count, cancellationToken);

        var leagues = leagueList
            .Select(league => new AdminLeagueListItemDto(
                league.Id, league.Tier, league.WeekStartDate, league.WeekEndDate,
                memberCountByLeagueId.GetValueOrDefault(league.Id, 0)))
            .ToList();

        var ordered = leagues
            .OrderByDescending(league => league.WeekStartDate)
            .ThenByDescending(league => tierOrderMap.GetValueOrDefault(league.Tier, -1))
            .ToList();

        return Ok(ordered);
    }

    [HttpGet("weeks")]
    public async Task<ActionResult<IReadOnlyList<DateOnly>>> GetWeeks(CancellationToken cancellationToken = default)
    {
        var weeks = await databaseContext.Leagues
            .Select(league => league.WeekStartDate)
            .Distinct()
            .OrderByDescending(weekStart => weekStart)
            .ToListAsync(cancellationToken);

        return Ok(weeks);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AdminLeagueDetailDto>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var detail = await BuildDetailAsync(id, cancellationToken);
        if (detail is null)
        {
            return NotFound();
        }

        return Ok(detail);
    }

    [HttpPost("close-current")]
    public async Task<IActionResult> CloseCurrentWeek(CancellationToken cancellationToken = default)
    {
        await leagueService.CloseCurrentLeagueAndCreateNextAsync(cancellationToken);
        logger.LogWarning("League week manually closed");
        return NoContent();
    }

    [HttpPost("{id:guid}/resync")]
    public async Task<ActionResult<AdminLeagueDetailDto>> Resync(Guid id, CancellationToken cancellationToken = default)
    {
        var leagueExists = await databaseContext.Leagues.AnyAsync(league => league.Id == id, cancellationToken);
        if (!leagueExists)
        {
            return NotFound();
        }

        await leagueService.SyncLeagueWeeklyExperiencePointsAsync(id, cancellationToken);
        logger.LogInformation("League XP resynced LeagueId={LeagueId}", id);
        return Ok(await BuildDetailAsync(id, cancellationToken));
    }

    [HttpPut("memberships/{membershipId:guid}/tier")]
    public async Task<ActionResult<AdminLeagueDetailDto>> MoveMembershipTier(
        Guid membershipId,
        [FromBody] MoveMembershipTierRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var tierKeys = await LoadTierKeysAsync(cancellationToken);
        if (!tierKeys.Contains(request.Tier))
        {
            return BadRequest(new { message = $"Unknown tier: {request.Tier}" });
        }

        var membership = await databaseContext.LeagueMemberships
            .FirstOrDefaultAsync(record => record.Id == membershipId, cancellationToken);
        if (membership is null)
        {
            return NotFound();
        }

        var currentLeague = await databaseContext.Leagues
            .FirstOrDefaultAsync(league => league.Id == membership.LeagueId, cancellationToken);
        if (currentLeague is null)
        {
            return NotFound();
        }

        if (currentLeague.Tier == request.Tier)
        {
            return Ok(await BuildDetailAsync(currentLeague.Id, cancellationToken));
        }

        var targetLeague = await databaseContext.Leagues
            .FirstOrDefaultAsync(league => league.WeekStartDate == currentLeague.WeekStartDate && league.Tier == request.Tier, cancellationToken);

        if (targetLeague is null)
        {
            targetLeague = new Features.League.Models.League
            {
                Id = Guid.NewGuid(),
                Tier = request.Tier,
                WeekStartDate = currentLeague.WeekStartDate,
                WeekEndDate = currentLeague.WeekEndDate,
            };
            databaseContext.Leagues.Add(targetLeague);
        }

        membership.LeagueId = targetLeague.Id;
        membership.Rank = 0;
        membership.PromotionOutcome = null;
        await databaseContext.SaveChangesAsync(cancellationToken);

        await leagueService.SyncLeagueWeeklyExperiencePointsAsync(targetLeague.Id, cancellationToken);

        logger.LogInformation(
            "League membership moved MembershipId={MembershipId} UserId={UserId} {OldTier} -> {NewTier}",
            membership.Id, membership.UserId, currentLeague.Tier, request.Tier);

        return Ok(await BuildDetailAsync(targetLeague.Id, cancellationToken));
    }

    [HttpPut("memberships/{membershipId:guid}/xp")]
    public async Task<ActionResult<AdminLeagueDetailDto>> AdjustMembershipXp(
        Guid membershipId,
        [FromBody] AdjustMembershipXpRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.Delta == 0)
        {
            return BadRequest(new { message = "Delta must be non-zero" });
        }

        var membership = await databaseContext.LeagueMemberships
            .FirstOrDefaultAsync(record => record.Id == membershipId, cancellationToken);
        if (membership is null)
        {
            return NotFound();
        }

        var league = await databaseContext.Leagues
            .FirstOrDefaultAsync(leagueRecord => leagueRecord.Id == membership.LeagueId, cancellationToken);
        if (league is null)
        {
            return NotFound();
        }

        databaseContext.UserExperiencePointsRecords.Add(new UserExperiencePointsRecord
        {
            Id = Guid.NewGuid(),
            UserId = membership.UserId,
            Amount = request.Delta,
            Source = ExperiencePointsSources.AdministratorCorrection,
            EarnedAt = league.WeekStartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
        });
        await databaseContext.SaveChangesAsync(cancellationToken);

        await leagueService.SyncLeagueWeeklyExperiencePointsAsync(league.Id, cancellationToken);

        logger.LogInformation(
            "League XP adjusted MembershipId={MembershipId} UserId={UserId} Delta={Delta}",
            membership.Id, membership.UserId, request.Delta);

        return Ok(await BuildDetailAsync(league.Id, cancellationToken));
    }

    [HttpDelete("memberships/{membershipId:guid}")]
    public async Task<IActionResult> RemoveMembership(Guid membershipId, CancellationToken cancellationToken = default)
    {
        var membership = await databaseContext.LeagueMemberships
            .FirstOrDefaultAsync(record => record.Id == membershipId, cancellationToken);
        if (membership is null)
        {
            return NotFound();
        }

        databaseContext.LeagueMemberships.Remove(membership);
        await databaseContext.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "League membership removed MembershipId={MembershipId} UserId={UserId} LeagueId={LeagueId}",
            membership.Id, membership.UserId, membership.LeagueId);

        return NoContent();
    }

    [HttpGet("settings")]
    public async Task<ActionResult<LeagueSettingsDto>> GetSettings(CancellationToken cancellationToken = default)
    {
        var settings = await leagueService.GetSettingsAsync(cancellationToken);
        return Ok(ToSettingsDto(settings));
    }

    [HttpPut("settings")]
    public async Task<ActionResult<LeagueSettingsDto>> UpdateSettings(
        [FromBody] UpdateLeagueSettingsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.MaximumLeagueParticipantCount <= 0 ||
            request.PromotionZoneSize <= 0 ||
            request.DemotionZoneSize <= 0)
        {
            return BadRequest(new { message = "All settings values must be positive" });
        }

        if (request.PromotionZoneSize + request.DemotionZoneSize > request.MaximumLeagueParticipantCount)
        {
            return BadRequest(new { message = "Promotion + demotion zones cannot exceed maximum participant count" });
        }

        if (request.PeriodLengthDays is <= 0)
        {
            return BadRequest(new { message = "Period length must be positive" });
        }

        var settings = await leagueService.GetSettingsAsync(cancellationToken);

        settings.MaximumLeagueParticipantCount = request.MaximumLeagueParticipantCount;
        settings.PromotionZoneSize = request.PromotionZoneSize;
        settings.DemotionZoneSize = request.DemotionZoneSize;

        if (request.PeriodLengthDays is { } periodLength)
        {
            settings.PeriodLengthDays = periodLength;
        }

        if (request.CurrentPeriodEndsAt is { } endsAt)
        {
            settings.CurrentPeriodEndsAt = endsAt;
            var newEndDate = DateOnly.FromDateTime(endsAt.UtcDateTime);
            var activeLeagues = await databaseContext.Leagues
                .Where(league => league.WeekStartDate == settings.CurrentPeriodStartDate)
                .ToListAsync(cancellationToken);
            foreach (var league in activeLeagues)
            {
                league.WeekEndDate = newEndDate;
            }
        }

        await databaseContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "League settings updated Max={Max} Promotion={Promotion} Demotion={Demotion} PeriodEndsAt={PeriodEndsAt} PeriodLength={PeriodLength}",
            settings.MaximumLeagueParticipantCount, settings.PromotionZoneSize, settings.DemotionZoneSize,
            settings.CurrentPeriodEndsAt, settings.PeriodLengthDays);

        return Ok(ToSettingsDto(settings));
    }

    private static LeagueSettingsDto ToSettingsDto(Features.League.Models.LeagueSettings settings) =>
        new(settings.MaximumLeagueParticipantCount,
            settings.PromotionZoneSize,
            settings.DemotionZoneSize,
            settings.CurrentPeriodEndsAt,
            settings.PeriodLengthDays);

    [HttpGet("tiers")]
    public async Task<ActionResult<IReadOnlyList<AdminLeagueTierDto>>> GetTiers(CancellationToken cancellationToken = default)
    {
        var tiers = await databaseContext.LeagueTiers
            .OrderBy(tier => tier.Order)
            .Select(tier => new AdminLeagueTierDto(tier.Id, tier.Key, tier.Name, tier.Color, tier.Order))
            .ToListAsync(cancellationToken);
        return Ok(tiers);
    }

    [HttpPost("tiers")]
    public async Task<ActionResult<AdminLeagueTierDto>> CreateTier(
        [FromBody] CreateLeagueTierRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var key = (request.Key ?? string.Empty).Trim().ToLowerInvariant();
        var validationMessage = ValidateTierFields(key, request.Name, request.Color);
        if (validationMessage is not null)
        {
            return BadRequest(new { message = validationMessage });
        }

        if (await databaseContext.LeagueTiers.AnyAsync(tier => tier.Key == key, cancellationToken))
        {
            return BadRequest(new { message = $"Tier with key '{key}' already exists" });
        }

        var leagueTier = new LeagueTier
        {
            Id = Guid.NewGuid(),
            Key = key,
            Name = request.Name.Trim(),
            Color = request.Color.Trim(),
            Order = request.Order,
        };
        databaseContext.LeagueTiers.Add(leagueTier);
        await databaseContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("League tier created Key={Key}", leagueTier.Key);

        return Ok(new AdminLeagueTierDto(leagueTier.Id, leagueTier.Key, leagueTier.Name, leagueTier.Color, leagueTier.Order));
    }

    [HttpPut("tiers/{id:guid}")]
    public async Task<ActionResult<AdminLeagueTierDto>> UpdateTier(
        Guid id,
        [FromBody] UpdateLeagueTierRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var validationMessage = ValidateTierFields(key: "x", request.Name, request.Color);
        if (validationMessage is not null)
        {
            return BadRequest(new { message = validationMessage });
        }

        var leagueTier = await databaseContext.LeagueTiers.FirstOrDefaultAsync(tier => tier.Id == id, cancellationToken);
        if (leagueTier is null)
        {
            return NotFound();
        }

        leagueTier.Name = request.Name.Trim();
        leagueTier.Color = request.Color.Trim();
        leagueTier.Order = request.Order;
        await databaseContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("League tier updated Key={Key}", leagueTier.Key);

        return Ok(new AdminLeagueTierDto(leagueTier.Id, leagueTier.Key, leagueTier.Name, leagueTier.Color, leagueTier.Order));
    }

    [HttpDelete("tiers/{id:guid}")]
    public async Task<IActionResult> DeleteTier(Guid id, CancellationToken cancellationToken = default)
    {
        var leagueTier = await databaseContext.LeagueTiers.FirstOrDefaultAsync(tier => tier.Id == id, cancellationToken);
        if (leagueTier is null)
        {
            return NotFound();
        }

        if (await databaseContext.LeagueTiers.CountAsync(cancellationToken) <= 1)
        {
            return BadRequest(new { message = "At least one tier must remain" });
        }

        if (await databaseContext.Leagues.AnyAsync(league => league.Tier == leagueTier.Key, cancellationToken))
        {
            return BadRequest(new { message = "Cannot delete a tier that has existing leagues; reassign members first" });
        }

        databaseContext.LeagueTiers.Remove(leagueTier);
        await databaseContext.SaveChangesAsync(cancellationToken);

        logger.LogWarning("League tier deleted Key={Key}", leagueTier.Key);

        return NoContent();
    }

    private static string? ValidateTierFields(string key, string? name, string? color)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "Key is required";
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return "Name is required";
        }

        if (string.IsNullOrWhiteSpace(color))
        {
            return "Color is required";
        }

        return null;
    }

    private async Task<AdminLeagueDetailDto?> BuildDetailAsync(Guid leagueId, CancellationToken cancellationToken)
    {
        var league = await databaseContext.Leagues.FirstOrDefaultAsync(record => record.Id == leagueId, cancellationToken);
        if (league is null)
        {
            return null;
        }

        var members = await databaseContext.LeagueMemberships
            .Where(membership => membership.LeagueId == leagueId)
            .Join(
                databaseContext.UserReplicas,
                membership => membership.UserId,
                user => user.UserId,
                (membership, user) => new { membership, user })
            .OrderByDescending(pair => pair.membership.WeeklyXpAmount)
            .Select(pair => new AdminLeagueMemberDto(
                pair.membership.Id,
                pair.user.UserId,
                pair.user.DisplayName,
                pair.user.Email,
                pair.membership.WeeklyXpAmount,
                pair.membership.Rank,
                pair.membership.PromotionOutcome))
            .ToListAsync(cancellationToken);

        return new AdminLeagueDetailDto(
            league.Id, league.Tier, league.WeekStartDate, league.WeekEndDate, members);
    }
}
