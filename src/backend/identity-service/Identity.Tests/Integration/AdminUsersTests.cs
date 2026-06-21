using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Infrastructure.Data;
using Sellevate.Identity.Tests.Helpers;

namespace Sellevate.Identity.Tests.Integration;

[TestFixture]
[Category("Integration")]
public class AdminUsersTests
{
    private TestWebApplicationFactory Factory => IntegrationTestSetup.Factory;

    private async Task<User> SeedUserAsync(UserRole role = UserRole.User)
    {
        using var scope = Factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = $"seed-{Guid.NewGuid():N}@test.com",
            DisplayName = "Seed User",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = true,
            Role = role,
            AvatarType = AvatarKind.Default
        };
        database.Users.Add(user);
        await database.SaveChangesAsync();
        return user;
    }

    [Test]
    public async Task List_RequiresAuthentication()
    {
        var client = Factory.CreateClient();
        var response = await client.GetAsync("/admin/users");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task List_ForbiddenForRegularUser()
    {
        var client = Factory.CreateAuthenticatedClient(Guid.NewGuid(), "u@test.com", "U", UserRole.User);
        var response = await client.GetAsync("/admin/users");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task List_AllowedForAdmin()
    {
        var client = Factory.CreateAuthenticatedClient(Guid.NewGuid(), "a@test.com", "A", UserRole.Admin);
        var response = await client.GetAsync("/admin/users");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task Detail_ForUnknownUser_ReturnsNotFound()
    {
        var client = Factory.CreateAuthenticatedClient(Guid.NewGuid(), "a@test.com", "A", UserRole.Admin);
        var response = await client.GetAsync($"/admin/users/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Rename_UpdatesDisplayName()
    {
        var target = await SeedUserAsync();
        var client = Factory.CreateAuthenticatedClient(Guid.NewGuid(), "a@test.com", "A", UserRole.Admin);

        var response = await client.PutAsJsonAsync($"/admin/users/{target.Id}",
            new { displayName = "Renamed Person" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AdminUserResponse>();
        body!.DisplayName.Should().Be("Renamed Person");
    }

    [Test]
    public async Task Rename_RejectsTooShortDisplayName()
    {
        var target = await SeedUserAsync();
        var client = Factory.CreateAuthenticatedClient(Guid.NewGuid(), "a@test.com", "A", UserRole.Admin);

        var response = await client.PutAsJsonAsync($"/admin/users/{target.Id}", new { displayName = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ChangeRole_ForbiddenForAdmin()
    {
        var target = await SeedUserAsync();
        var client = Factory.CreateAuthenticatedClient(Guid.NewGuid(), "a@test.com", "A", UserRole.Admin);

        var response = await client.PutAsJsonAsync($"/admin/users/{target.Id}/role", new { role = "Admin" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task ChangeRole_AllowedForSuperAdmin()
    {
        var target = await SeedUserAsync();
        var client = Factory.CreateAuthenticatedClient(Guid.NewGuid(), "s@test.com", "S", UserRole.SuperAdmin);

        var response = await client.PutAsJsonAsync($"/admin/users/{target.Id}/role", new { role = "Admin" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AdminUserResponse>();
        body!.Role.Should().Be("Admin");
    }

    [Test]
    public async Task ChangeRole_RejectsUnknownRole()
    {
        var target = await SeedUserAsync();
        var client = Factory.CreateAuthenticatedClient(Guid.NewGuid(), "s@test.com", "S", UserRole.SuperAdmin);

        var response = await client.PutAsJsonAsync($"/admin/users/{target.Id}/role", new { role = "Wizard" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private sealed record AdminUserResponse(Guid Id, string Email, string DisplayName, string Role);
}
