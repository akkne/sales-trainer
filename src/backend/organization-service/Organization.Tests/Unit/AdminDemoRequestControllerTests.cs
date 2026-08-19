using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Organization.Features.DemoRequests.Endpoints;
using Sellevate.Organization.Features.DemoRequests.Models;
using Sellevate.Organization.Features.DemoRequests.Services.Abstract;

namespace Sellevate.Organization.Tests.Unit;

[TestFixture]
public sealed class AdminDemoRequestControllerTests
{
    private IDemoRequestService _demoRequestService = null!;
    private AdminDemoRequestController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _demoRequestService = Substitute.For<IDemoRequestService>();
        _controller = new AdminDemoRequestController(_demoRequestService);
    }

    private static DemoRequestDto SampleDto(Guid? id = null, DateTime? createdAt = null) => new(
        id ?? Guid.NewGuid(),
        "Jane Doe",
        "jane@example.com",
        null,
        "Acme Inc",
        null,
        nameof(SalesTeamSize.SixToTwenty),
        null,
        nameof(DemoRequestStatus.New),
        createdAt ?? DateTime.UtcNow,
        null,
        createdAt ?? DateTime.UtcNow,
        createdAt ?? DateTime.UtcNow);

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
}
