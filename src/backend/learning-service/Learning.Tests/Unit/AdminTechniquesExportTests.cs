using System.Security.Claims;
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
    /// <summary>
    /// Phase 40.18: the controller admits organization administrators too, and refuses import/create to
    /// anyone who is not Sellevate platform staff. These tests exercise the platform-staff path, so the
    /// principal has to say so.
    /// </summary>
    private static AdminTechniquesController CreateController(Sellevate.Learning.Infrastructure.Data.LearningDbContext databaseContext) =>
        new(databaseContext, NullLogger<AdminTechniquesController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = PlatformAdministrator() },
            },
        };

    private static ClaimsPrincipal PlatformAdministrator() =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Role, "Admin")], "test"));

    /// <summary>
    /// The export re-imports verbatim, so it is asserted in the import payload's own shape and in the
    /// export's guaranteed order — by sort order, not by insertion.
    /// </summary>
    [Test]
    public async Task Export_ReturnsAllTechniques_InImportableShape()
    {
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();
        databaseContext.Techniques.Add(new Technique
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
        databaseContext.Techniques.Add(new Technique
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
        await databaseContext.SaveChangesAsync();

        var result = await CreateController(databaseContext).Export(CancellationToken.None);

        var payload = (result.Result as OkObjectResult)!.Value as IReadOnlyList<AdminTechniqueWriteRequestDto>;
        payload.Should().NotBeNull();
        payload!.Should().HaveCount(2);
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
        await using var databaseContext = LearningDbContextFactory.CreateInMemory();
        var techniqueId = Guid.NewGuid();
        databaseContext.Techniques.Add(new Technique
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
        await databaseContext.SaveChangesAsync();

        var result = await CreateController(databaseContext).Export(CancellationToken.None);

        var payload = (result.Result as OkObjectResult)!.Value as IReadOnlyList<AdminTechniqueWriteRequestDto>;
        payload!.Single().Coach.Should().NotBeNull();
        payload.Single().Coach!.Name.Should().Be("Coach");
        payload.Single().Coach!.Challenges.Should().NotBeNull();
    }
}
