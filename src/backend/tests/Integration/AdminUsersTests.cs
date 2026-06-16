using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Tests;
using SalesTrainer.Tests.Helpers;

namespace SalesTrainer.Tests.Integration;

[TestFixture]
public class AdminUsersTests
{
    private AppDbContext _db = null!;
    private IServiceScope _scope = null!;
    private HttpClient _superAdminClient = null!;
    private HttpClient _adminClient = null!;

    [SetUp]
    public async Task SetUp()
    {
        _scope = IntegrationTestSetup.Factory.Services.CreateScope();
        _db = _scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var superAdmin = await TestDbSeeder.SeedUserAsync(_db,
            email: $"sa_{Guid.NewGuid()}@test.com", role: UserRole.SuperAdmin);
        var admin = await TestDbSeeder.SeedUserAsync(_db,
            email: $"adm_{Guid.NewGuid()}@test.com", role: UserRole.Admin);

        _superAdminClient = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            superAdmin.Id, superAdmin.Email, superAdmin.DisplayName, UserRole.SuperAdmin);
        _adminClient = IntegrationTestSetup.Factory.CreateAuthenticatedClient(
            admin.Id, admin.Email, admin.DisplayName, UserRole.Admin);
    }

    [TearDown]
    public void TearDown() => _scope.Dispose();

    [Test]
    public async Task GetAll_AsSuperAdmin_Returns200WithUsers()
    {
        var response = await _superAdminClient.GetAsync("/admin/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await response.Content.ReadFromJsonAsync<JsonElement>();
        list.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Test]
    public async Task GetAll_AsAdmin_Returns200()
    {
        // Regular admin can also list users (policy is RequireAdmin, not SuperAdmin)
        var response = await _adminClient.GetAsync("/admin/users");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Test]
    public async Task ChangeRole_AsSuperAdmin_Returns200WithNewRole()
    {
        var target = await TestDbSeeder.SeedUserAsync(_db,
            email: $"target_{Guid.NewGuid()}@test.com", role: UserRole.User);

        var response = await _superAdminClient.PutAsJsonAsync(
            $"/admin/users/{target.Id}/role",
            new { role = "Admin" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("role").GetString().Should().Be("Admin");
    }

    [Test]
    public async Task ChangeRole_AsAdmin_Returns403()
    {
        var target = await TestDbSeeder.SeedUserAsync(_db,
            email: $"target2_{Guid.NewGuid()}@test.com", role: UserRole.User);

        var response = await _adminClient.PutAsJsonAsync(
            $"/admin/users/{target.Id}/role",
            new { role = "Admin" });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task ChangeRole_InvalidRole_Returns400()
    {
        var target = await TestDbSeeder.SeedUserAsync(_db,
            email: $"target3_{Guid.NewGuid()}@test.com");

        var response = await _superAdminClient.PutAsJsonAsync(
            $"/admin/users/{target.Id}/role",
            new { role = "InvalidRole" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task ChangeRole_NonExistentUser_Returns404()
    {
        var response = await _superAdminClient.PutAsJsonAsync(
            $"/admin/users/{Guid.NewGuid()}/role",
            new { role = "Admin" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GetById_ReturnsRicherUserDetail()
    {
        var target = await TestDbSeeder.SeedUserAsync(_db,
            email: $"detail_{Guid.NewGuid()}@test.com", role: UserRole.User);

        var response = await _adminClient.GetAsync($"/admin/users/{target.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("email").GetString().Should().Be(target.Email);
        body.GetProperty("authProvider").GetString().Should().Be("Password");
        body.GetProperty("hasCustomAvatar").GetBoolean().Should().BeFalse();
        body.TryGetProperty("totalXpAmount", out _).Should().BeTrue();
        body.TryGetProperty("currentStreakDayCount", out _).Should().BeTrue();
    }

    [Test]
    public async Task GetById_NonExistentUser_Returns404()
    {
        var response = await _adminClient.GetAsync($"/admin/users/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task UpdateUser_AsAdmin_RenamesDisplayName()
    {
        var target = await TestDbSeeder.SeedUserAsync(_db,
            email: $"rename_{Guid.NewGuid()}@test.com", displayName: "Old Name");

        var response = await _adminClient.PutAsJsonAsync(
            $"/admin/users/{target.Id}",
            new { displayName = "Clean Name" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("displayName").GetString().Should().Be("Clean Name");
    }

    [Test]
    public async Task UpdateUser_TooShortName_Returns400()
    {
        var target = await TestDbSeeder.SeedUserAsync(_db,
            email: $"short_{Guid.NewGuid()}@test.com");

        var response = await _adminClient.PutAsJsonAsync(
            $"/admin/users/{target.Id}",
            new { displayName = "x" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task UpdateUser_NonExistentUser_Returns404()
    {
        var response = await _adminClient.PutAsJsonAsync(
            $"/admin/users/{Guid.NewGuid()}",
            new { displayName = "Whoever" });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeleteAvatar_AsAdmin_Returns204()
    {
        var target = await TestDbSeeder.SeedUserAsync(_db,
            email: $"avatar_{Guid.NewGuid()}@test.com");

        var response = await _adminClient.DeleteAsync($"/admin/users/{target.Id}/avatar");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Test]
    public async Task DeleteAvatar_NonExistentUser_Returns404()
    {
        var response = await _adminClient.DeleteAsync($"/admin/users/{Guid.NewGuid()}/avatar");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
