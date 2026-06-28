using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using Sellevate.Ai.Features.Dialog;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Infrastructure.Data;

namespace Sellevate.Ai.Tests.Unit;

[TestFixture]
public sealed class AdminDialogExportTests
{
    private static AiDbContext CreateInMemory()
    {
        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AiDbContext(options);
    }

    private static AdminDialogController CreateController(AiDbContext db) =>
        new(db, NullLogger<AdminDialogController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

    [Test]
    public async Task Export_ReturnsBundlesWithNestedModes_InImportableShape()
    {
        await using var db = CreateInMemory();
        var skillId = Guid.NewGuid();
        var bundleId = Guid.NewGuid();
        db.DialogBundles.Add(new DialogBundle
        {
            Id = bundleId,
            SkillId = skillId,
            Title = "Cold call simulator",
            Description = "desc",
            IconEmoji = "📞",
            SortOrder = 1,
            IsActive = true,
        });
        db.DialogModes.Add(new DialogMode
        {
            Id = Guid.NewGuid(),
            BundleId = bundleId,
            Key = "secretary-bypass",
            Title = "Get past the gatekeeper",
            Description = "d",
            ChatSystemPrompt = "chat",
            FeedbackSystemPrompt = "feedback",
            SortOrder = 1,
            IsActive = true,
            VoiceEnabled = false,
            VoiceId = null,
        });
        await db.SaveChangesAsync();

        var result = await CreateController(db).Export(CancellationToken.None);

        var export = (result.Result as OkObjectResult)!.Value as DialogExportDto;
        export.Should().NotBeNull();
        export!.Bundles.Should().HaveCount(1);

        var bundle = export.Bundles[0];
        bundle.SkillId.Should().Be(skillId);
        bundle.Title.Should().Be("Cold call simulator");
        bundle.Modes.Should().HaveCount(1);
        bundle.Modes[0].Key.Should().Be("secretary-bypass");
        bundle.Modes[0].ChatSystemPrompt.Should().Be("chat");
        bundle.Modes[0].FeedbackSystemPrompt.Should().Be("feedback");
    }

    [Test]
    public async Task Export_OrdersBundlesBySortOrder()
    {
        await using var db = CreateInMemory();
        db.DialogBundles.Add(new DialogBundle
        {
            Id = Guid.NewGuid(), SkillId = Guid.NewGuid(), Title = "Second",
            Description = "", IconEmoji = "💬", SortOrder = 2, IsActive = true,
        });
        db.DialogBundles.Add(new DialogBundle
        {
            Id = Guid.NewGuid(), SkillId = Guid.NewGuid(), Title = "First",
            Description = "", IconEmoji = "💬", SortOrder = 1, IsActive = true,
        });
        await db.SaveChangesAsync();

        var result = await CreateController(db).Export(CancellationToken.None);

        var export = (result.Result as OkObjectResult)!.Value as DialogExportDto;
        export!.Bundles.Select(b => b.Title).Should().ContainInOrder("First", "Second");
    }
}
