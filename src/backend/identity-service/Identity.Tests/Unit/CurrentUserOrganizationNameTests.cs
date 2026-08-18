using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;
using Sellevate.Identity.Common.Constants;
using Sellevate.Identity.Features.Auth;
using Sellevate.Identity.Features.Organizations.Models;
using Sellevate.Identity.Infrastructure.Data;
using Sellevate.Identity.Tests.Helpers;

namespace Sellevate.Identity.Tests.Unit;

/// <summary>
/// <c>GET /auth/me</c> — the <c>orgName</c> field added in Phase 40.20 so the organization panel can
/// say whose panel it is.
///
/// <para>
/// The name is read from the local registry projection on every call rather than minted into the
/// token, and the two tests that matter are the consequences of that choice: a rename shows up
/// without anybody signing in again, and a member whose projection has not caught up yet gets
/// <see langword="null"/> — a neutral label in the panel — rather than an error on the one endpoint
/// every page load depends on.
/// </para>
/// </summary>
[TestFixture]
public sealed class CurrentUserOrganizationNameTests
{
    private static readonly Guid OrganizationId = Guid.Parse("cccccccc-0000-4000-8000-000000000033");

    private IdentityDbContext _databaseContext = null!;

    [SetUp]
    public void SetUp() => _databaseContext = InMemoryDbContextFactory.Create($"auth-me-{Guid.NewGuid()}");

    [TearDown]
    public void TearDown() => _databaseContext.Dispose();

    [Test]
    public async Task The_name_comes_from_the_registry_projection_not_from_the_token()
    {
        _databaseContext.OrganizationReplicas.Add(new OrganizationReplica
        {
            OrganizationId = OrganizationId,
            Name = "Acme Sales",
            Slug = "acme",
            UpdatedAt = DateTime.UnixEpoch,
        });
        await _databaseContext.SaveChangesAsync();

        var payload = await CurrentUserAsync(OrganizationId.ToString());

        ValueOf(payload, "orgName").Should().Be("Acme Sales");
    }

    [Test]
    public async Task A_member_whose_projection_has_not_caught_up_gets_null_rather_than_an_error()
    {
        var payload = await CurrentUserAsync(OrganizationId.ToString());

        ValueOf(payload, "orgName").Should().BeNull();
        ValueOf(payload, "orgId").Should().Be(OrganizationId.ToString());
    }

    [Test]
    public async Task Platform_staff_with_no_organization_claim_get_null_and_no_lookup()
    {
        var payload = await CurrentUserAsync(organizationId: null);

        ValueOf(payload, "orgName").Should().BeNull();
        ValueOf(payload, "orgId").Should().BeNull();
    }

    /// <summary>
    /// A malformed claim must not throw on the endpoint every authenticated page load calls.
    /// </summary>
    [Test]
    public async Task An_unparseable_organization_claim_yields_null_rather_than_throwing()
    {
        var payload = await CurrentUserAsync("not-a-guid");

        ValueOf(payload, "orgName").Should().BeNull();
    }

    private async Task<object> CurrentUserAsync(string? organizationId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new(ClaimTypes.Email, "member@example.com"),
            new(ClaimTypeNames.DisplayName, "Участник"),
            new(ClaimTypes.Role, "User"),
        };

        if (organizationId is not null)
        {
            claims.Add(new Claim(ClaimTypeNames.OrganizationId, organizationId));
        }

        var controller = new AuthController(
            authenticationService: null!,
            inviteService: null!,
            _databaseContext,
            new StubWebHostEnvironment())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test")),
                },
            },
        };

        var result = await controller.GetCurrentUser();
        return ((OkObjectResult)result).Value!;
    }

    private static string? ValueOf(object payload, string propertyName)
        => payload.GetType().GetProperty(propertyName)!.GetValue(payload) as string;

    private sealed class StubWebHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
