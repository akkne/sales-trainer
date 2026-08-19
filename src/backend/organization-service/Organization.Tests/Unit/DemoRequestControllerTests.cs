using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Organization.Features.DemoRequests.Endpoints;
using Sellevate.Organization.Features.DemoRequests.Exceptions;
using Sellevate.Organization.Features.DemoRequests.Models;
using Sellevate.Organization.Features.DemoRequests.Services.Abstract;

namespace Sellevate.Organization.Tests.Unit;

[TestFixture]
public sealed class DemoRequestControllerTests
{
    private IDemoRequestService _demoRequestService = null!;
    private DemoRequestController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _demoRequestService = Substitute.For<IDemoRequestService>();
        _controller = new DemoRequestController(_demoRequestService)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }

    private static CreateDemoRequestRequestDto SampleRequest() => new(
        FullName: "Jane Doe",
        WorkEmail: "jane@example.com",
        Phone: null,
        CompanyName: "Acme Inc",
        JobTitle: null,
        SalesTeamSize: SalesTeamSize.SixToTwenty,
        Comment: null,
        ConsentGiven: true,
        MarketingConsentGiven: false,
        Website: null);

    [Test]
    public async Task SubmitDemoRequest_returns_202_with_the_accepted_dto()
    {
        var accepted = new DemoRequestAcceptedDto(Guid.NewGuid(), DateTime.UtcNow);
        _demoRequestService.SubmitAsync(Arg.Any<CreateDemoRequestRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(accepted);

        var result = await _controller.SubmitDemoRequest(SampleRequest(), CancellationToken.None);

        var acceptedResult = result.Result.Should().BeOfType<AcceptedResult>().Subject;
        acceptedResult.Value.Should().Be(accepted);
    }

    [Test]
    public async Task SubmitDemoRequest_returns_429_with_retry_after_on_cooldown()
    {
        _demoRequestService.SubmitAsync(Arg.Any<CreateDemoRequestRequestDto>(), Arg.Any<CancellationToken>())
            .Returns<DemoRequestAcceptedDto>(_ => throw new DemoRequestCooldownException(42));

        var result = await _controller.SubmitDemoRequest(SampleRequest(), CancellationToken.None);

        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        _controller.Response.Headers.RetryAfter.ToString().Should().Be("42");
    }
}
