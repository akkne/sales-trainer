using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NUnit.Framework;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Organization.Features.Organizations.Endpoints;
using Sellevate.Organization.Features.Organizations.Models;
using Sellevate.Organization.Features.Organizations.Services.Abstract;

namespace Sellevate.Organization.Tests.Unit;

[TestFixture]
public sealed class OrganizationProfileControllerTests
{
    private IOrganizationProfileService _organizationProfileService = null!;
    private OrganizationProfileController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _organizationProfileService = Substitute.For<IOrganizationProfileService>();
        _controller = new OrganizationProfileController(_organizationProfileService);
    }

    private static OrganizationProfileDto SampleProfile() => new(
        "Sales training SaaS", "B2B sales teams", [], [], "consultative",
        new Dictionary<string, string>(), [], DateTime.UtcNow, DateTime.UtcNow);

    [Test]
    public async Task GetProfile_returns_404_when_the_organization_has_no_profile_yet()
    {
        _organizationProfileService.GetProfileAsync(Arg.Any<CancellationToken>()).Returns((OrganizationProfileDto?)null);

        var result = await _controller.GetProfile(CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public async Task GetProfile_returns_200_with_the_profile()
    {
        var profile = SampleProfile();
        _organizationProfileService.GetProfileAsync(Arg.Any<CancellationToken>()).Returns(profile);

        var result = await _controller.GetProfile(CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(profile);
    }

    [Test]
    public async Task UpdateProfile_returns_200_with_the_upserted_profile()
    {
        var profile = SampleProfile();
        _organizationProfileService.UpsertProfileAsync(Arg.Any<UpdateOrganizationProfileRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(profile);

        var request = new UpdateOrganizationProfileRequestDto(null, null, null, null, null, null, null);
        var result = await _controller.UpdateProfile(request, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(profile);
    }

    [Test]
    public void Controller_carries_the_TenantScoped_attribute_so_the_middleware_rejects_missing_headers()
    {
        typeof(OrganizationProfileController).Should().BeDecoratedWith<TenantScopedAttribute>();
    }

    [Test]
    public void Controller_route_has_no_organization_id_segment()
    {
        var routeAttribute = typeof(OrganizationProfileController)
            .GetCustomAttributes(typeof(RouteAttribute), inherit: false)
            .Cast<RouteAttribute>()
            .Single();

        var forbiddenRouteParameterName = string.Join(string.Empty, "organization", "Id");
        routeAttribute.Template.Should().NotContain($"{{{forbiddenRouteParameterName}}}");
        routeAttribute.Template.Should().NotContain("{id");
    }
}
