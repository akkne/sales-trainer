using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;
using Sellevate.Identity.Features.Onboarding.Services.Implementation;
using Sellevate.Identity.Tests.Helpers;

namespace Sellevate.Identity.Tests.Unit;

[TestFixture]
public class OnboardingServiceTests
{
    [Test]
    public async Task CompleteOnboarding_CreatesProfile_WhenNoneExists()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var service = new OnboardingService(db);
        var userId = Guid.NewGuid();

        await service.CompleteOnboardingForUserAsync(userId, "outbound", "beginner", "sdr");

        var profile = await db.UserProfiles.SingleAsync(p => p.UserId == userId);
        profile.IsOnboardingCompleted.Should().BeTrue();
        profile.SalesType.Should().Be("outbound");
        profile.ExperienceLevel.Should().Be("beginner");
        profile.Persona.Should().Be("sdr");
    }

    [Test]
    public async Task CompleteOnboarding_IsIdempotent_OnceCompleted()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var service = new OnboardingService(db);
        var userId = Guid.NewGuid();

        await service.CompleteOnboardingForUserAsync(userId, "outbound", "beginner");
        await service.CompleteOnboardingForUserAsync(userId, "inbound", "expert");

        var profile = await db.UserProfiles.SingleAsync(p => p.UserId == userId);
        // The second call must be a no-op: the original values are preserved.
        profile.SalesType.Should().Be("outbound");
        profile.ExperienceLevel.Should().Be("beginner");
    }
}
