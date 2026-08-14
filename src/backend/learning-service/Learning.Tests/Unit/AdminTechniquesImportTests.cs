using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Sellevate.Learning.Features.Admin;
using Sellevate.Learning.Features.SkillTree.Models;
using Sellevate.Learning.Features.Techniques;
using Sellevate.Learning.Infrastructure.Data;

namespace Sellevate.Learning.Tests.Unit;

/// <summary>
/// Re-importing an existing technique used to blow up: the coach row (and the skill
/// links) were deleted and re-inserted inside one SaveChanges, which EF rejects.
/// These tests pin the in-place sync behaviour.
/// </summary>
[TestFixture]
public sealed class AdminTechniquesImportTests
{
    private static AdminTechniquesController CreateController(LearningDbContext db) =>
        new(db, NullLogger<AdminTechniquesController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

    private static AdminTechniqueWriteRequestDto BuildPayload(
        string coachName = "Dana Cross",
        Guid[]? additionalSkillIds = null,
        AdminTechniqueCoachDto? coach = null,
        bool withoutCoach = false) =>
        new(
            Slug: "mirroring",
            Name: "Mirroring",
            Summary: "summary",
            Body: "body",
            Tags: ["rapport"],
            PrimarySkillId: null,
            AdditionalSkillIds: additionalSkillIds ?? [],
            Difficulty: TechniqueLevels.Practitioner,
            SortOrder: 1,
            Dialog: null,
            Case: null,
            Coach: withoutCoach
                ? null
                : coach ?? new AdminTechniqueCoachDto("dana", coachName, "Coach", "Quote", null));

    private static async Task<AdminTechniqueImportResultDto> ImportAsync(
        LearningDbContext db, params AdminTechniqueWriteRequestDto[] payload)
    {
        var result = await CreateController(db).Import(payload, CancellationToken.None);
        return ((result.Result as OkObjectResult)!.Value as AdminTechniqueImportResultDto)!;
    }

    [Test]
    public async Task Import_TechniqueWithCoach_IsReImportable()
    {
        await using var db = LearningDbContextFactory.CreateInMemory();

        var created = await ImportAsync(db, BuildPayload());
        created.CreatedCount.Should().Be(1);
        created.FailedCount.Should().Be(0);

        var updated = await ImportAsync(db, BuildPayload(coachName: "Dana Cross II"));

        updated.UpdatedCount.Should().Be(1);
        updated.CreatedCount.Should().Be(0);
        updated.FailedCount.Should().Be(0);
        updated.Errors.Should().BeEmpty();

        db.Techniques.Should().HaveCount(1);
        var coaches = await db.TechniqueCoaches.ToListAsync();
        coaches.Should().ContainSingle();
        coaches[0].Name.Should().Be("Dana Cross II");
    }

    [Test]
    public async Task Import_WithoutCoach_RemovesTheExistingCoach()
    {
        await using var db = LearningDbContextFactory.CreateInMemory();
        await ImportAsync(db, BuildPayload());

        var result = await ImportAsync(db, BuildPayload(withoutCoach: true));

        result.FailedCount.Should().Be(0);
        db.TechniqueCoaches.Should().BeEmpty();
    }

    [Test]
    public async Task Import_AdditionalSkills_AreSyncedWithoutDuplicates()
    {
        await using var db = LearningDbContextFactory.CreateInMemory();
        var keptSkillId = Guid.NewGuid();
        var droppedSkillId = Guid.NewGuid();
        var addedSkillId = Guid.NewGuid();
        db.Skills.AddRange(
            new Skill { Id = keptSkillId, IconicName = "kept", Title = "Kept" },
            new Skill { Id = droppedSkillId, IconicName = "dropped", Title = "Dropped" },
            new Skill { Id = addedSkillId, IconicName = "added", Title = "Added" });
        await db.SaveChangesAsync();

        await ImportAsync(db, BuildPayload(additionalSkillIds: [keptSkillId, droppedSkillId]));

        var result = await ImportAsync(db, BuildPayload(additionalSkillIds: [keptSkillId, addedSkillId]));

        result.FailedCount.Should().Be(0);
        var links = await db.TechniqueSkills.Select(link => link.SkillId).ToListAsync();
        links.Should().BeEquivalentTo(new[] { keptSkillId, addedSkillId });
    }

    [Test]
    public async Task Import_CountsFailedRowsOnlyOnce()
    {
        await using var db = LearningDbContextFactory.CreateInMemory();

        // Difficulty outside 1..4 fails validation before anything is written.
        var invalid = BuildPayload() with { Difficulty = 9 };
        var result = await ImportAsync(db, invalid);

        result.FailedCount.Should().Be(1);
        result.CreatedCount.Should().Be(0);
        result.UpdatedCount.Should().Be(0);
        db.Techniques.Should().BeEmpty();
    }
}
