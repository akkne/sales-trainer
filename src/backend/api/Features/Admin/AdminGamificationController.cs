using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesTrainer.Api.Features.Admin.Models;
using SalesTrainer.Api.Features.Gamification.Models;
using SalesTrainer.Api.Features.Gamification.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Api.Features.Admin;

/// <summary>
/// Moderation surface for the database-driven XP/gamification configuration:
/// daily/weekly goals, dialog scoring, per-exercise-type base XP, and streak milestones.
/// </summary>
[ApiController]
[Route("admin/gamification")]
[Authorize(Policy = "RequireAdmin")]
public sealed class AdminGamificationController(
    AppDbContext database,
    IGamificationService gamificationService,
    ILogger<AdminGamificationController> logger) : ControllerBase
{
    // --- Settings ---

    [HttpGet("settings")]
    public async Task<ActionResult<GamificationSettingsDto>> GetSettings()
    {
        var settings = await gamificationService.GetSettingsAsync();
        return Ok(ToSettingsDto(settings));
    }

    [HttpPut("settings")]
    public async Task<ActionResult<GamificationSettingsDto>> UpdateSettings(
        [FromBody] UpdateGamificationSettingsRequestDto request)
    {
        if (request.DailyXpGoal <= 0 || request.WeeklyXpGoal <= 0)
            return BadRequest(new { message = "Daily and weekly XP goals must be positive" });

        if (request.DialogXpMultiplier <= 0)
            return BadRequest(new { message = "Dialog XP multiplier must be positive" });

        if (request.DialogWeightConfidence < 0 || request.DialogWeightStructure < 0 ||
            request.DialogWeightObjection < 0 || request.DialogWeightGoal < 0)
            return BadRequest(new { message = "Dialog criterion weights cannot be negative" });

        var totalWeight = request.DialogWeightConfidence + request.DialogWeightStructure +
                          request.DialogWeightObjection + request.DialogWeightGoal;
        if (totalWeight <= 0)
            return BadRequest(new { message = "The sum of dialog criterion weights must be positive" });

        var settings = await gamificationService.GetSettingsAsync();
        settings.DailyXpGoal = request.DailyXpGoal;
        settings.WeeklyXpGoal = request.WeeklyXpGoal;
        settings.DialogXpMultiplier = request.DialogXpMultiplier;
        settings.DialogWeightConfidence = request.DialogWeightConfidence;
        settings.DialogWeightStructure = request.DialogWeightStructure;
        settings.DialogWeightObjection = request.DialogWeightObjection;
        settings.DialogWeightGoal = request.DialogWeightGoal;
        await database.SaveChangesAsync();

        logger.LogInformation(
            "Gamification settings updated DailyGoal={Daily} WeeklyGoal={Weekly} DialogMultiplier={Multiplier} by ActorId={ActorId}",
            settings.DailyXpGoal, settings.WeeklyXpGoal, settings.DialogXpMultiplier,
            User.FindFirstValue(ClaimTypes.NameIdentifier));

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

    // --- Exercise-type base XP ---

    [HttpGet("exercise-rewards")]
    public async Task<ActionResult<List<ExerciseTypeRewardDto>>> GetExerciseRewards()
    {
        var rewards = await database.ExerciseTypeRewards
            .OrderBy(r => r.ExerciseType)
            .Select(r => new ExerciseTypeRewardDto(r.Id, r.ExerciseType, r.BaseXpReward))
            .ToListAsync();
        return Ok(rewards);
    }

    [HttpPut("exercise-rewards/{exerciseType}")]
    public async Task<ActionResult<ExerciseTypeRewardDto>> UpdateExerciseReward(
        string exerciseType, [FromBody] UpdateExerciseTypeRewardRequestDto request)
    {
        if (request.BaseXpReward < 0)
            return BadRequest(new { message = "Base XP reward cannot be negative" });

        var reward = await database.ExerciseTypeRewards
            .FirstOrDefaultAsync(r => r.ExerciseType == exerciseType);

        if (reward is null)
        {
            reward = new ExerciseTypeReward
            {
                Id = Guid.NewGuid(),
                ExerciseType = exerciseType,
                BaseXpReward = request.BaseXpReward
            };
            database.ExerciseTypeRewards.Add(reward);
        }
        else
        {
            reward.BaseXpReward = request.BaseXpReward;
        }

        await database.SaveChangesAsync();

        logger.LogInformation("Exercise reward updated Type={ExerciseType} BaseXp={BaseXp} by ActorId={ActorId}",
            reward.ExerciseType, reward.BaseXpReward, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new ExerciseTypeRewardDto(reward.Id, reward.ExerciseType, reward.BaseXpReward));
    }

    // --- Streak milestones ---

    [HttpGet("streak-milestones")]
    public async Task<ActionResult<List<StreakMilestoneDto>>> GetStreakMilestones()
    {
        var milestones = await database.StreakMilestones
            .OrderBy(m => m.DayCount)
            .Select(m => new StreakMilestoneDto(m.Id, m.DayCount, m.XpReward))
            .ToListAsync();
        return Ok(milestones);
    }

    [HttpPost("streak-milestones")]
    public async Task<ActionResult<StreakMilestoneDto>> CreateStreakMilestone(
        [FromBody] SaveStreakMilestoneRequestDto request)
    {
        var validation = ValidateMilestone(request);
        if (validation is not null) return BadRequest(new { message = validation });

        if (await database.StreakMilestones.AnyAsync(m => m.DayCount == request.DayCount))
            return BadRequest(new { message = $"A milestone for {request.DayCount} days already exists" });

        var milestone = new StreakMilestone
        {
            Id = Guid.NewGuid(),
            DayCount = request.DayCount,
            XpReward = request.XpReward
        };
        database.StreakMilestones.Add(milestone);
        await database.SaveChangesAsync();

        logger.LogInformation("Streak milestone created Days={Days} Xp={Xp} by ActorId={ActorId}",
            milestone.DayCount, milestone.XpReward, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new StreakMilestoneDto(milestone.Id, milestone.DayCount, milestone.XpReward));
    }

    [HttpPut("streak-milestones/{id:guid}")]
    public async Task<ActionResult<StreakMilestoneDto>> UpdateStreakMilestone(
        Guid id, [FromBody] SaveStreakMilestoneRequestDto request)
    {
        var validation = ValidateMilestone(request);
        if (validation is not null) return BadRequest(new { message = validation });

        var milestone = await database.StreakMilestones.FirstOrDefaultAsync(m => m.Id == id);
        if (milestone is null) return NotFound();

        if (await database.StreakMilestones.AnyAsync(m => m.DayCount == request.DayCount && m.Id != id))
            return BadRequest(new { message = $"A milestone for {request.DayCount} days already exists" });

        milestone.DayCount = request.DayCount;
        milestone.XpReward = request.XpReward;
        await database.SaveChangesAsync();

        logger.LogInformation("Streak milestone updated Id={Id} Days={Days} Xp={Xp} by ActorId={ActorId}",
            milestone.Id, milestone.DayCount, milestone.XpReward, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return Ok(new StreakMilestoneDto(milestone.Id, milestone.DayCount, milestone.XpReward));
    }

    [HttpDelete("streak-milestones/{id:guid}")]
    public async Task<IActionResult> DeleteStreakMilestone(Guid id)
    {
        var milestone = await database.StreakMilestones.FirstOrDefaultAsync(m => m.Id == id);
        if (milestone is null) return NotFound();

        database.StreakMilestones.Remove(milestone);
        await database.SaveChangesAsync();

        logger.LogWarning("Streak milestone deleted Id={Id} Days={Days} by ActorId={ActorId}",
            milestone.Id, milestone.DayCount, User.FindFirstValue(ClaimTypes.NameIdentifier));

        return NoContent();
    }

    private static string? ValidateMilestone(SaveStreakMilestoneRequestDto request)
    {
        if (request.DayCount <= 0) return "Day count must be positive";
        if (request.XpReward < 0) return "XP reward cannot be negative";
        return null;
    }
}
