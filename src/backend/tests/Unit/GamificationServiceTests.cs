using FluentAssertions;
using NUnit.Framework;
using SalesTrainer.Api.Features.Gamification.Models;
using SalesTrainer.Api.Features.Gamification.Services.Implementation;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Tests.Helpers;

namespace SalesTrainer.Tests.Unit;

[TestFixture]
public class GamificationServiceTests
{
    private AppDbContext _db = null!;
    private GamificationService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _db = InMemoryDbContextFactory.Create();
        _service = new GamificationService(_db);
    }

    [TearDown]
    public void TearDown() => _db.Dispose();

    [Test]
    public async Task GetSettings_WhenNoneExist_CreatesRowWithDefaults()
    {
        var settings = await _service.GetSettingsAsync();

        settings.DailyXpGoal.Should().Be(100);
        settings.WeeklyXpGoal.Should().Be(500);
        settings.DialogXpMultiplier.Should().Be(1.0);
        settings.DialogWeightConfidence.Should().Be(25);
        _db.GamificationSettings.Should().HaveCount(1);
    }

    [Test]
    public async Task GetSettings_IsIdempotent_DoesNotCreateDuplicateRows()
    {
        await _service.GetSettingsAsync();
        await _service.GetSettingsAsync();

        _db.GamificationSettings.Should().HaveCount(1);
    }

    [Test]
    public async Task GetExerciseBaseXp_WhenNoRow_FallsBackToDefaultTen()
    {
        var xp = await _service.GetExerciseBaseXpAsync("choose_option");

        xp.Should().Be(10);
    }

    [Test]
    public async Task GetExerciseBaseXp_WhenRowExists_UsesConfiguredValue()
    {
        _db.ExerciseTypeRewards.Add(new ExerciseTypeReward
        {
            Id = Guid.NewGuid(), ExerciseType = "free_text", BaseXpReward = 42
        });
        await _db.SaveChangesAsync();

        var xp = await _service.GetExerciseBaseXpAsync("free_text");

        xp.Should().Be(42);
    }

    [Test]
    public async Task GetStreakBonus_WhenTableEmpty_FallsBackToHistoricLadder()
    {
        (await _service.GetStreakBonusXpAsync(7)).Should().Be(50);
        (await _service.GetStreakBonusXpAsync(30)).Should().Be(200);
        (await _service.GetStreakBonusXpAsync(3)).Should().Be(0);
    }

    [Test]
    public async Task GetStreakBonus_WhenConfigured_UsesDbAndIgnoresFallback()
    {
        _db.StreakMilestones.Add(new StreakMilestone { Id = Guid.NewGuid(), DayCount = 5, XpReward = 15 });
        await _db.SaveChangesAsync();

        // Configured milestone is honored...
        (await _service.GetStreakBonusXpAsync(5)).Should().Be(15);
        // ...and the historic 7/30 fallback no longer applies once the table is non-empty.
        (await _service.GetStreakBonusXpAsync(7)).Should().Be(0);
    }
}
