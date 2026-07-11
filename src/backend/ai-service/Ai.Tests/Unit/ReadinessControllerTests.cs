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
public class ReadinessControllerTests
{
    private IReadinessService _readinessService = null!;
    private ReadinessController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _readinessService = Substitute.For<IReadinessService>();
        _controller = new ReadinessController(_readinessService, NullLogger<ReadinessController>.Instance);
    }

    private static GenerateReadinessRequestDto ValidRequest() =>
        new(Guid.NewGuid(), "Записать встречу", ["s1", "s2"]);

    [Test]
    public async Task GenerateReadiness_ReturnsOkWithReadiness()
    {
        _readinessService
            .GenerateReadinessAsync(Arg.Any<GenerateReadinessRequestDto>(), Arg.Any<CancellationToken>())
            .Returns(new ReadinessResultDto(80, ["Уверенность"], ["Цена"], "Продолжайте практиковаться."));

        var result = await _controller.GenerateReadiness(ValidRequest());

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ReadinessResultDto>().Subject;
        body.Score.Should().Be(80);
    }

    [Test]
    public async Task GenerateReadiness_ReturnsNoContent_WhenServiceSignalsNoData()
    {
        _readinessService
            .GenerateReadinessAsync(Arg.Any<GenerateReadinessRequestDto>(), Arg.Any<CancellationToken>())
            .Returns((ReadinessResultDto?)null);

        var result = await _controller.GenerateReadiness(ValidRequest());

        result.Should().BeOfType<NoContentResult>();
    }

    [Test]
    public async Task GenerateReadiness_ReturnsBadRequest_WhenSessionIdsExceedLimit()
    {
        var oversized = new GenerateReadinessRequestDto(Guid.NewGuid(), null, Enumerable.Range(0, 51).Select(i => $"s{i}").ToList());

        var result = await _controller.GenerateReadiness(oversized);

        result.Should().BeOfType<BadRequestObjectResult>();
        await _readinessService.DidNotReceive()
            .GenerateReadinessAsync(Arg.Any<GenerateReadinessRequestDto>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task GenerateReadiness_ReturnsServiceUnavailable_OnInvalidOperationException()
    {
        _readinessService
            .GenerateReadinessAsync(Arg.Any<GenerateReadinessRequestDto>(), Arg.Any<CancellationToken>())
            .Returns<ReadinessResultDto?>(_ => throw new InvalidOperationException("not configured"));

        var result = await _controller.GenerateReadiness(ValidRequest());

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(503);
    }

    [Test]
    public async Task GenerateReadiness_ReturnsServiceUnavailable_OnHttpRequestException()
    {
        _readinessService
            .GenerateReadinessAsync(Arg.Any<GenerateReadinessRequestDto>(), Arg.Any<CancellationToken>())
            .Returns<ReadinessResultDto?>(_ => throw new HttpRequestException("provider error"));

        var result = await _controller.GenerateReadiness(ValidRequest());

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(503);
    }
}
