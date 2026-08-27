using System.Reflection;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Organization.Common.Constants;
using Sellevate.Organization.Features.DemoRequests.Constants;
using Sellevate.Organization.Features.DemoRequests.Endpoints;
using Sellevate.Organization.Features.DemoRequests.Exceptions;
using Sellevate.Organization.Features.DemoRequests.Models;
using Sellevate.Organization.Features.DemoRequests.Services.Abstract;
using Sellevate.Organization.Features.Organizations.Exceptions;

namespace Sellevate.Organization.Tests.Unit;

[TestFixture]
public sealed class AdminDemoRequestControllerTests
{
    private static readonly Guid ActorUserId = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");

    private IDemoRequestService _demoRequestService = null!;
    private IDemoRequestProvisioningService _demoRequestProvisioningService = null!;
    private AdminDemoRequestController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _demoRequestService = Substitute.For<IDemoRequestService>();
        _demoRequestProvisioningService = Substitute.For<IDemoRequestProvisioningService>();
        _controller = new AdminDemoRequestController(_demoRequestService, _demoRequestProvisioningService);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, ActorUserId.ToString()),
                ])),
            },
        };
    }

    private static DemoRequestDto SampleDto(Guid? id = null, DateTime? createdAt = null) => new(
        id ?? Guid.NewGuid(),
        "Jane Doe",
        "jane@example.com",
        "+7 900 123-45-67",
        "Acme Inc",
        null,
        nameof(SalesTeamSize.SixToTwenty),
        null,
        nameof(DemoRequestStatus.New),
        createdAt ?? DateTime.UtcNow,
        null,
        createdAt ?? DateTime.UtcNow,
        createdAt ?? DateTime.UtcNow,
        null,
        null,
        null,
        nameof(DemoRequestProvisioningState.NotProvisioned),
        null,
        null,
        null);

    private static DemoRequestProvisioningResultDto SampleProvisioningResult(Guid? demoRequestId = null) => new(
        demoRequestId ?? Guid.NewGuid(),
        nameof(DemoRequestStatus.Approved),
        nameof(DemoRequestProvisioningState.AdminInvited),
        new ProvisionedOrganizationDto(Guid.NewGuid(), "Acme Sales", "acme-sales"),
        Guid.NewGuid(),
        "admin@example.com",
        DateTime.UtcNow.AddDays(7),
        AlreadyProvisioned: false);

    [Test]
    public async Task ListDemoRequests_returns_200_with_the_newest_first_list_from_the_service()
    {
        var newer = SampleDto(createdAt: DateTime.UtcNow);
        var older = SampleDto(createdAt: DateTime.UtcNow.AddDays(-1));
        _demoRequestService.ListDemoRequestsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<DemoRequestDto> { newer, older });

        var result = await _controller.ListDemoRequests(CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(new[] { newer, older }, options => options.WithStrictOrdering());
    }

    [Test]
    public async Task UpdateDemoRequestStatus_returns_200_with_the_updated_dto()
    {
        var updated = SampleDto();
        _demoRequestService.UpdateStatusAsync(updated.Id, DemoRequestStatus.Contacted, Arg.Any<CancellationToken>())
            .Returns(updated);

        var result = await _controller.UpdateDemoRequestStatus(
            updated.Id, new UpdateDemoRequestStatusRequestDto(DemoRequestStatus.Contacted), CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(updated);
    }

    [Test]
    public async Task UpdateDemoRequestStatus_returns_404_when_unknown_id()
    {
        _demoRequestService.UpdateStatusAsync(Arg.Any<Guid>(), Arg.Any<DemoRequestStatus>(), Arg.Any<CancellationToken>())
            .Returns((DemoRequestDto?)null);

        var result = await _controller.UpdateDemoRequestStatus(
            Guid.NewGuid(), new UpdateDemoRequestStatusRequestDto(DemoRequestStatus.Declined), CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public async Task ProvisionDemoRequest_returns_200_with_the_provisioning_result()
    {
        var demoRequestId = Guid.NewGuid();
        var provisioningResult = SampleProvisioningResult(demoRequestId);
        _demoRequestProvisioningService.ProvisionAsync(
                demoRequestId, Arg.Any<ProvisionDemoRequestRequestDto>(), ActorUserId, Arg.Any<CancellationToken>())
            .Returns(provisioningResult);

        var result = await _controller.ProvisionDemoRequest(demoRequestId, null, CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(provisioningResult);
    }

    [Test]
    public async Task ProvisionDemoRequest_returns_404_for_an_unknown_lead()
    {
        _demoRequestProvisioningService.ProvisionAsync(
                Arg.Any<Guid>(), Arg.Any<ProvisionDemoRequestRequestDto>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((DemoRequestProvisioningResultDto?)null);

        var result = await _controller.ProvisionDemoRequest(Guid.NewGuid(), null, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Test]
    public async Task ProvisionDemoRequest_returns_409_slug_taken_on_a_slug_conflict()
    {
        _demoRequestProvisioningService.ProvisionAsync(
                Arg.Any<Guid>(), Arg.Any<ProvisionDemoRequestRequestDto>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<DemoRequestProvisioningResultDto?>(_ => throw new OrganizationSlugConflictException("acme"));

        var result = await _controller.ProvisionDemoRequest(Guid.NewGuid(), null, CancellationToken.None);

        var conflictResult = result.Result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflictResult.Value.Should().BeEquivalentTo(new
        {
            code = DemoRequestProvisioningConstants.SlugTakenCode,
            slug = "acme",
            message = "An organization with slug 'acme' already exists.",
        });
    }

    [Test]
    public async Task ProvisionDemoRequest_returns_503_invite_failed()
    {
        var organizationId = Guid.NewGuid();
        _demoRequestProvisioningService.ProvisionAsync(
                Arg.Any<Guid>(), Arg.Any<ProvisionDemoRequestRequestDto>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<DemoRequestProvisioningResultDto?>(_ => throw new DemoRequestInviteFailedException(organizationId));

        var result = await _controller.ProvisionDemoRequest(Guid.NewGuid(), null, CancellationToken.None);

        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        objectResult.Value.Should().BeEquivalentTo(new
        {
            code = DemoRequestProvisioningConstants.InviteFailedCode,
            organizationId,
            provisioningState = nameof(DemoRequestProvisioningState.OrganizationCreated),
        });
    }

    [Test]
    public async Task ProvisionDemoRequest_returns_400_for_a_bad_role_or_email()
    {
        _demoRequestProvisioningService.ProvisionAsync(
                Arg.Any<Guid>(), Arg.Any<ProvisionDemoRequestRequestDto>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<DemoRequestProvisioningResultDto?>(_ => throw new ArgumentException("Unknown organization role: Owner"));

        var result = await _controller.ProvisionDemoRequest(Guid.NewGuid(), null, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Test]
    public void AdminDemoRequestController_is_gated_by_RequirePlatformAdministrator()
    {
        typeof(AdminDemoRequestController).GetCustomAttribute<AuthorizeAttribute>()?.Policy.Should().Be(
            AuthorizationPolicies.RequirePlatformAdministrator,
            "the lead list and the status update stay ordinary platform administration");
    }

    [Test]
    public void ProvisionDemoRequest_action_carries_RequireSuperAdministrator()
    {
        typeof(AdminDemoRequestController)
            .GetMethod(nameof(AdminDemoRequestController.ProvisionDemoRequest))!
            .GetCustomAttribute<AuthorizeAttribute>()?.Policy.Should().Be(
                AuthorizationPolicies.RequireSuperAdministrator,
                "provisioning creates a membership, the one privilege reserved for a superadmin — "
                + "and an action attribute is ANDed with the controller's, so the looser class gate "
                + "cannot widen it back down");
    }

    [Test]
    public void UpdateDemoRequestStatus_action_carries_no_tighter_gate_of_its_own()
    {
        typeof(AdminDemoRequestController)
            .GetMethod(nameof(AdminDemoRequestController.UpdateDemoRequestStatus))!
            .GetCustomAttribute<AuthorizeAttribute>().Should().BeNull(
                "/status stays RequirePlatformAdministrator, inherited from the controller");
    }
}
