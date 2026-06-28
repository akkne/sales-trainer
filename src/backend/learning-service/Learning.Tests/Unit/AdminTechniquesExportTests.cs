using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Sellevate.Learning.Features.Admin;
using Sellevate.Learning.Features.Techniques;
using Sellevate.Learning.Features.Techniques.Models;

namespace Sellevate.Learning.Tests.Unit;

[TestFixture]
public sealed class AdminTechniquesExportTests
{
    private static AdminTechniquesController CreateController(Sellevate.Learning.Infrastructure.Data.LearningDbContext db) =>
        new(db, NullLogger<AdminTechniquesController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

    [Test]
    public async Task Export_ReturnsAllTechniques_InImportableShape()
    {
        await using var db = LearningDbContextFactory.CreateInMemory();
        db.Techniques.Add(new Technique
        {
            Id = Guid.NewGuid(),
            Slug = "mirroring",
            Name = "Mirroring",
            Summary = "summary",
            Body = "body",
            Tags = ["objection", "rapport"],
            Difficulty = TechniqueLevels.Practitioner,
            SortOrder = 2,
            DialogJson = """[{"side":"me","text":"hi"}]""",
            CaseJson = """{"title":"case"}""",
        });
        db.Techniques.Add(new Technique
        {
            Id = Guid.NewGuid(),
            Slug = "anchoring",
            Name = "Anchoring",
            Summary = "",
            Body = "",
            Tags = [],
            Difficulty = TechniqueLevels.Novice,
            SortOrder = 1,
        });
        await db.SaveChangesAsync();

        var result = await CreateController(db).Export(CancellationToken.None);

        var payload = (result.Result as OkObjectResult)!.Value as IReadOnlyList<AdminTechniqueWriteRequestDto>;
        payload.Should().NotBeNull();
        payload!.Should().HaveCount(2);
        // Ordered by SortOrder.
        payload[0].Slug.Should().Be("anchoring");
        payload[1].Slug.Should().Be("mirroring");

        var mirroring = payload[1];
        mirroring.Tags.Should().BeEquivalentTo(new[] { "objection", "rapport" });
        mirroring.Difficulty.Should().Be(TechniqueLevels.Practitioner);
        mirroring.Dialog.Should().NotBeNull();
        mirroring.Case.Should().NotBeNull();
    }

    [Test]
    public async Task Export_PreservesCoach()
    {
        await using var db = LearningDbContextFactory.CreateInMemory();
        var techniqueId = Guid.NewGuid();
        db.Techniques.Add(new Technique
        {
            Id = techniqueId,
            Slug = "framing",
            Name = "Framing",
            Summary = "",
            Body = "",
            Tags = [],
            Difficulty = TechniqueLevels.Novice,
            SortOrder = 1,
            Coach = new TechniqueCoach
            {
                Id = Guid.NewGuid(),
                TechniqueId = techniqueId,
                AvatarSeed = "seed",
                Name = "Coach",
                Role = "Mentor",
                Quote = "Practice.",
                ChallengesJson = """[{"label":"do it"}]""",
            },
        });
        await db.SaveChangesAsync();

        var result = await CreateController(db).Export(CancellationToken.None);

        var payload = (result.Result as OkObjectResult)!.Value as IReadOnlyList<AdminTechniqueWriteRequestDto>;
        payload!.Single().Coach.Should().NotBeNull();
        payload.Single().Coach!.Name.Should().Be("Coach");
        payload.Single().Coach!.Challenges.Should().NotBeNull();
    }
}
