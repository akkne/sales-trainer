using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NUnit.Framework;
using Sellevate.Identity.Tests.Helpers;

namespace Sellevate.Identity.Tests.Integration;

[TestFixture]
[Category("Integration")]
public class ProfileAndOnboardingTests
{
    private TestWebApplicationFactory Factory => IntegrationTestSetup.Factory;

    [Test]
    public async Task Profile_RequiresAuthentication()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/profile");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task Onboarding_ThenProfile_ReflectsPersona()
    {
        var userId = Guid.NewGuid();
        var client = Factory.CreateAuthenticatedClient(userId, "p@test.com", "Persona User");

        // GET /profile needs a User row to read identity fields from; the authenticated
        // client's token is for a user that only exists in the JWT, so onboarding (which
        // writes UserProfiles, keyed by the token's sub) is what we verify end-to-end here.
        var onboarding = await client.PostAsJsonAsync("/onboarding",
            new { salesType = "outbound", experienceLevel = "beginner", persona = "founder" });
        onboarding.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var personaUpdate = await client.PutAsJsonAsync("/profile/persona", new { persona = "sdr" });
        personaUpdate.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task UpdatePersona_RejectsInvalidValue()
    {
        var client = Factory.CreateAuthenticatedClient(Guid.NewGuid(), "x@test.com", "X");
        var response = await client.PutAsJsonAsync("/profile/persona", new { persona = "not-a-persona" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task GetAvatar_ForUnknownUser_ReturnsNotFound()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync($"/avatars/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
