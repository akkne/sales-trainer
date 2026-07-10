using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Ai.Features.Companies;
using Sellevate.Ai.Features.Companies.Models;
using Sellevate.Ai.Features.Companies.Services.Abstract;

namespace Sellevate.Ai.Tests.Unit;

[TestFixture]
public class BriefingControllerTests
{
    private IBriefingService _briefingService = null!;
    private BriefingController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _briefingService = Substitute.For<IBriefingService>();
        _controller = new BriefingController(_briefingService, NullLogger<BriefingController>.Instance);
    }

    private static GenerateBriefingRequestDto ValidRequest() =>
        new("Описание компании", "Цель звонка", [], []);

    [Test]
    public async Task GenerateBriefing_ReturnsOkWithContent()
    {
        _briefingService
            .GenerateBriefingAsync(Arg.Any<GenerateBriefingRequestDto>(), Arg.Any<CancellationToken>())
            .Returns("## Кто они\n- Тестовая компания");

        var result = await _controller.GenerateBriefing(ValidRequest());

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<BriefingResultDto>().Subject;
        body.Content.Should().Be("## Кто они\n- Тестовая компания");
    }

    [Test]
    public async Task GenerateBriefing_ReturnsBadRequest_WhenContextExceedsLimit()
    {
        var oversized = new GenerateBriefingRequestDto(
            new string('a', 60001), null, [], []);

        var result = await _controller.GenerateBriefing(oversized);

        result.Should().BeOfType<BadRequestObjectResult>();
        await _briefingService.DidNotReceive()
            .GenerateBriefingAsync(Arg.Any<GenerateBriefingRequestDto>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GenerateBriefing_ReturnsServiceUnavailable_OnInvalidOperationException()
    {
        _briefingService
            .GenerateBriefingAsync(Arg.Any<GenerateBriefingRequestDto>(), Arg.Any<CancellationToken>())
            .Returns<string>(_ => throw new InvalidOperationException("not configured"));

        var result = await _controller.GenerateBriefing(ValidRequest());

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(503);
    }

    [Test]
    public async Task GenerateBriefing_ReturnsServiceUnavailable_OnHttpRequestException()
    {
        _briefingService
            .GenerateBriefingAsync(Arg.Any<GenerateBriefingRequestDto>(), Arg.Any<CancellationToken>())
            .Returns<string>(_ => throw new HttpRequestException("provider error"));

        var result = await _controller.GenerateBriefing(ValidRequest());

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(503);
    }
}
