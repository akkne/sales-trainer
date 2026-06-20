using FluentAssertions;
using NUnit.Framework;
using Sellevate.Learning.Features.Techniques;
using Sellevate.Learning.Features.Techniques.Models;
using Sellevate.Learning.Features.Techniques.Services.Implementation;

namespace Sellevate.Learning.Tests.Unit;

[TestFixture]
public sealed class TechniqueServiceTests
{
    private static Technique CreateTechnique(string slug, Guid? primarySkillId = null) => new()
    {
        Id = Guid.NewGuid(),
        Slug = slug,
        Name = slug,
        Summary = "summary",
        Body = "body",
        Tags = ["objection"],
        PrimarySkillId = primarySkillId,
        Difficulty = TechniqueLevels.Novice,
        SortOrder = 1,
    };

    [Test]
    public async Task GetTechniqueCards_NewTechnique_IsFlaggedAsNew()
    {
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();
        databaseContext.Techniques.Add(CreateTechnique("mirroring"));
        await databaseContext.SaveChangesAsync();

        var service = new TechniqueService(databaseContext);

        var cards = await service.GetTechniqueCardsAsync(Guid.NewGuid(), null, null, null);

        cards.Should().HaveCount(1);
        cards[0].IsNew.Should().BeTrue();
        cards[0].MasteryLevel.Should().Be(0);
    }

    [Test]
    public async Task MarkTechniqueSeen_CreatesProgress_OnFirstView()
    {
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();
        var technique = CreateTechnique("anchoring");
        databaseContext.Techniques.Add(technique);
        await databaseContext.SaveChangesAsync();

        var service = new TechniqueService(databaseContext);
        var userId = Guid.NewGuid();

        await service.MarkTechniqueSeenAsync("anchoring", userId);

        var progress = databaseContext.UserTechniqueProgressRecords
            .SingleOrDefault(record => record.UserId == userId && record.TechniqueId == technique.Id);
        progress.Should().NotBeNull();
        progress!.Level.Should().Be(0);
    }

    [Test]
    public async Task MarkTechniqueSeen_IsIdempotent_OnRepeatViews()
    {
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();
        var technique = CreateTechnique("framing");
        databaseContext.Techniques.Add(technique);
        await databaseContext.SaveChangesAsync();

        var service = new TechniqueService(databaseContext);
        var userId = Guid.NewGuid();

        await service.MarkTechniqueSeenAsync("framing", userId);
        await service.MarkTechniqueSeenAsync("framing", userId);

        databaseContext.UserTechniqueProgressRecords
            .Count(record => record.UserId == userId && record.TechniqueId == technique.Id)
            .Should().Be(1);
    }
}
