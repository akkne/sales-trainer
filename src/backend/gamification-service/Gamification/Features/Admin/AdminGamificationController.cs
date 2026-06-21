using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sellevate.Gamification.Common.Constants;
using Sellevate.Gamification.Eventing;
using Sellevate.Gamification.Features.Admin.Models;
using Sellevate.Gamification.Features.Gamification.Models;
using Sellevate.Gamification.Features.Gamification.Services.Abstract;
using Sellevate.Gamification.Infrastructure.Data;

namespace Sellevate.Gamification.Features.Admin;

[ApiController]
[Route(RouteConstants.AdminGamification)]
[Authorize(Policy = AuthorizationPolicies.RequireAdministrator)]
public sealed class AdminGamificationController(
    GamificationDbContext databaseContext,
    IGamificationSettingsService gamificationSettingsService,
    IGamificationEventPublisher eventPublisher,
    ILogger<AdminGamificationController> logger) : ControllerBase
{
    [HttpGet("settings")]
    public async Task<ActionResult<GamificationSettingsDto>> GetSettings(CancellationToken cancellationToken = default)
    {
        var settings = await gamificationSettingsService.GetSettingsAsync(cancellationToken);
        return Ok(ToSettingsDto(settings));
    }

    [HttpPut("settings")]
    public async Task<ActionResult<GamificationSettingsDto>> UpdateSettings(
        [FromBody] UpdateGamificationSettingsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.DailyXpGoal <= 0 || request.WeeklyXpGoal <= 0)
        {
            return BadRequest(new { message = "Daily and weekly XP goals must be positive" });
        }

        if (request.DialogXpMultiplier <= 0)
        {
            return BadRequest(new { message = "Dialog XP multiplier must be positive" });
        }

        if (request.DialogWeightConfidence < 0 || request.DialogWeightStructure < 0 ||
            request.DialogWeightObjection < 0 || request.DialogWeightGoal < 0)
        {
            return BadRequest(new { message = "Dialog criterion weights cannot be negative" });
        }

        var totalWeight = request.DialogWeightConfidence + request.DialogWeightStructure +
                          request.DialogWeightObjection + request.DialogWeightGoal;
        if (totalWeight <= 0)
        {
            return BadRequest(new { message = "The sum of dialog criterion weights must be positive" });
        }

        var settings = await gamificationSettingsService.GetSettingsAsync(cancellationToken);
        settings.DailyXpGoal = request.DailyXpGoal;
        settings.WeeklyXpGoal = request.WeeklyXpGoal;
        settings.DialogXpMultiplier = request.DialogXpMultiplier;
        settings.DialogWeightConfidence = request.DialogWeightConfidence;
        settings.DialogWeightStructure = request.DialogWeightStructure;
        settings.DialogWeightObjection = request.DialogWeightObjection;
        settings.DialogWeightGoal = request.DialogWeightGoal;

        await eventPublisher.PublishDialogWeightsUpdatedAsync(
            new GamificationDialogWeightsUpdatedEvent(
                settings.DialogWeightConfidence,
                settings.DialogWeightStructure,
                settings.DialogWeightObjection,
                settings.DialogWeightGoal,
                settings.DialogXpMultiplier),
            cancellationToken);

        await databaseContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Gamification settings updated DailyGoal={Daily} WeeklyGoal={Weekly} DialogMultiplier={Multiplier}",
            settings.DailyXpGoal, settings.WeeklyXpGoal, settings.DialogXpMultiplier);

        return Ok(ToSettingsDto(settings));
    }

    private static GamificationSettingsDto ToSettingsDto(GamificationSettings settings) =>
        new(settings.DailyXpGoal,
            settings.WeeklyXpGoal,
            settings.DialogXpMultiplier,
            settings.DialogWeightConfidence,
            settings.DialogWeightStructure,
            settings.DialogWeightObjection,
            settings.DialogWeightGoal);

    [HttpGet("exercise-rewards")]
    public async Task<ActionResult<IReadOnlyList<ExerciseTypeRewardDto>>> GetExerciseRewards(CancellationToken cancellationToken = default)
    {
        var rewards = await databaseContext.ExerciseTypeRewards
            .OrderBy(reward => reward.ExerciseType)
            .Select(reward => new ExerciseTypeRewardDto(reward.Id, reward.ExerciseType, reward.BaseXpReward))
            .ToListAsync(cancellationToken);
        return Ok(rewards);
    }

    [HttpPut("exercise-rewards/{exerciseType}")]
    public async Task<ActionResult<ExerciseTypeRewardDto>> UpdateExerciseReward(
        string exerciseType,
        [FromBody] UpdateExerciseTypeRewardRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (request.BaseXpReward < 0)
        {
            return BadRequest(new { message = "Base XP reward cannot be negative" });
        }

        var reward = await databaseContext.ExerciseTypeRewards
            .FirstOrDefaultAsync(record => record.ExerciseType == exerciseType, cancellationToken);

        if (reward is null)
        {
            reward = new ExerciseTypeReward
            {
                Id = Guid.NewGuid(),
                ExerciseType = exerciseType,
                BaseXpReward = request.BaseXpReward,
            };
            databaseContext.ExerciseTypeRewards.Add(reward);
        }
        else
        {
            reward.BaseXpReward = request.BaseXpReward;
        }

        await databaseContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Exercise reward updated Type={ExerciseType} BaseXp={BaseXp}",
            reward.ExerciseType, reward.BaseXpReward);

        return Ok(new ExerciseTypeRewardDto(reward.Id, reward.ExerciseType, reward.BaseXpReward));
    }

    [HttpGet("streak-milestones")]
    public async Task<ActionResult<IReadOnlyList<StreakMilestoneDto>>> GetStreakMilestones(CancellationToken cancellationToken = default)
    {
        var milestones = await databaseContext.StreakMilestones
            .OrderBy(milestone => milestone.DayCount)
            .Select(milestone => new StreakMilestoneDto(milestone.Id, milestone.DayCount, milestone.XpReward))
            .ToListAsync(cancellationToken);
        return Ok(milestones);
    }

    [HttpPost("streak-milestones")]
    public async Task<ActionResult<StreakMilestoneDto>> CreateStreakMilestone(
        [FromBody] SaveStreakMilestoneRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var validationMessage = ValidateMilestone(request);
        if (validationMessage is not null)
        {
            return BadRequest(new { message = validationMessage });
        }

        if (await databaseContext.StreakMilestones.AnyAsync(milestone => milestone.DayCount == request.DayCount, cancellationToken))
        {
            return BadRequest(new { message = $"A milestone for {request.DayCount} days already exists" });
        }

        var milestone = new StreakMilestone
        {
            Id = Guid.NewGuid(),
            DayCount = request.DayCount,
            XpReward = request.XpReward,
        };
        databaseContext.StreakMilestones.Add(milestone);
        await databaseContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Streak milestone created Days={Days} Xp={Xp}", milestone.DayCount, milestone.XpReward);

        return Ok(new StreakMilestoneDto(milestone.Id, milestone.DayCount, milestone.XpReward));
    }

    [HttpPut("streak-milestones/{id:guid}")]
    public async Task<ActionResult<StreakMilestoneDto>> UpdateStreakMilestone(
        Guid id,
        [FromBody] SaveStreakMilestoneRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var validationMessage = ValidateMilestone(request);
        if (validationMessage is not null)
        {
            return BadRequest(new { message = validationMessage });
        }

        var milestone = await databaseContext.StreakMilestones.FirstOrDefaultAsync(record => record.Id == id, cancellationToken);
        if (milestone is null)
        {
            return NotFound();
        }

        if (await databaseContext.StreakMilestones.AnyAsync(record => record.DayCount == request.DayCount && record.Id != id, cancellationToken))
        {
            return BadRequest(new { message = $"A milestone for {request.DayCount} days already exists" });
        }

        milestone.DayCount = request.DayCount;
        milestone.XpReward = request.XpReward;
        await databaseContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Streak milestone updated Id={Id} Days={Days} Xp={Xp}", milestone.Id, milestone.DayCount, milestone.XpReward);

        return Ok(new StreakMilestoneDto(milestone.Id, milestone.DayCount, milestone.XpReward));
    }

    [HttpDelete("streak-milestones/{id:guid}")]
    public async Task<IActionResult> DeleteStreakMilestone(Guid id, CancellationToken cancellationToken = default)
    {
        var milestone = await databaseContext.StreakMilestones.FirstOrDefaultAsync(record => record.Id == id, cancellationToken);
        if (milestone is null)
        {
            return NotFound();
        }

        databaseContext.StreakMilestones.Remove(milestone);
        await databaseContext.SaveChangesAsync(cancellationToken);

        logger.LogWarning("Streak milestone deleted Id={Id} Days={Days}", milestone.Id, milestone.DayCount);

        return NoContent();
    }

    private static string? ValidateMilestone(SaveStreakMilestoneRequestDto request)
    {
        if (request.DayCount <= 0)
        {
            return "Day count must be positive";
        }

        if (request.XpReward < 0)
        {
            return "XP reward cannot be negative";
        }

        return null;
    }
}
