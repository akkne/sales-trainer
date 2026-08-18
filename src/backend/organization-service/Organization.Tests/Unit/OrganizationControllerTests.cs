using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Organization.Features.Organizations.Endpoints;
using Sellevate.Organization.Features.Organizations.Exceptions;
using Sellevate.Organization.Features.Organizations.Models;
using Sellevate.Organization.Features.Organizations.Services.Abstract;

namespace Sellevate.Organization.Tests.Unit;

[TestFixture]
public sealed class OrganizationControllerTests
{
    private IOrganizationService _organizationService = null!;
    private OrganizationController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _organizationService = Substitute.For<IOrganizationService>();
        _controller = new OrganizationController(_organizationService);
    }

    private static OrganizationDetailDto SampleDetail(Guid? id = null) => new(
        id ?? Guid.NewGuid(), "Acme", "acme", nameof(OrganizationStatus.Active), DateTime.UtcNow, DateTime.UtcNow);

    [Test]
    public async Task CreateOrganization_returns_201_with_the_created_organization()
    {
        var detail = SampleDetail();
        _organizationService.CreateOrganizationAsync(Arg.Any<CreateOrganizationRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(detail);

        var result = await _controller.CreateOrganization(new CreateOrganizationRequestDto("Acme", null), CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedResult>().Subject;
        createdResult.Value.Should().Be(detail);
    }

    [Test]
    public async Task CreateOrganization_returns_409_on_slug_conflict()
    {
        _organizationService.CreateOrganizationAsync(Arg.Any<CreateOrganizationRequestDto>(), Arg.Any<CancellationToken>())
            .Returns<OrganizationDetailDto>(_ => throw new OrganizationSlugConflictException("acme"));

        var result = await _controller.CreateOrganization(new CreateOrganizationRequestDto("Acme", "acme"), CancellationToken.None);

        result.Result.Should().BeOfType<ConflictObjectResult>();
    }

    [Test]
    public async Task CreateOrganization_returns_400_on_blank_name()
    {
        _organizationService.CreateOrganizationAsync(Arg.Any<CreateOrganizationRequestDto>(), Arg.Any<CancellationToken>())
            .Returns<OrganizationDetailDto>(_ => throw new ArgumentException("Organization name is required."));

        var result = await _controller.CreateOrganization(new CreateOrganizationRequestDto("  ", null), CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Test]
    public async Task GetOrganization_returns_404_when_missing()
    {
        _organizationService.GetOrganizationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((OrganizationDetailDto?)null);

        var result = await _controller.GetOrganization(Guid.NewGuid(), CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public async Task GetOrganization_returns_200_when_found()
    {
        var detail = SampleDetail();
        _organizationService.GetOrganizationAsync(detail.Id, Arg.Any<CancellationToken>()).Returns(detail);

        var result = await _controller.GetOrganization(detail.Id, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(detail);
    }

    [Test]
    public async Task SuspendOrganization_returns_404_when_missing()
    {
        _organizationService.SuspendOrganizationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((OrganizationDetailDto?)null);

        var result = await _controller.SuspendOrganization(Guid.NewGuid(), CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public async Task ReactivateOrganization_returns_200_with_active_status()
    {
        var id = Guid.NewGuid();
        var reactivated = new OrganizationDetailDto(id, "Acme", "acme", nameof(OrganizationStatus.Active), DateTime.UtcNow, DateTime.UtcNow);
        _organizationService.ReactivateOrganizationAsync(id, Arg.Any<CancellationToken>()).Returns(reactivated);

        var result = await _controller.ReactivateOrganization(id, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(reactivated);
    }
}
