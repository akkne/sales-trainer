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
public class ParseLogControllerTests
{
    private IParseLogService _parseLogService = null!;
    private ParseLogController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _parseLogService = Substitute.For<IParseLogService>();
        _controller = new ParseLogController(_parseLogService, NullLogger<ParseLogController>.Instance);
    }

    [Test]
    public async Task ParseLog_ReturnsOkWithParsedFields()
    {
        _parseLogService
            .ParseLogAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ParsedCallLogDto("Иван Петров", "Обсудили условия", "Взял паузу", new DateTime(2026, 7, 1)));

        var result = await _controller.ParseLog(new ParseCallLogRequestDto("сырые заметки"));

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ParsedCallLogDto>().Subject;
        body.ContactName.Should().Be("Иван Петров");
        body.Subject.Should().Be("Обсудили условия");
    }

    [Test]
    public async Task ParseLog_ReturnsBadRequest_WhenRawTextExceedsLimit()
    {
        var request = new ParseCallLogRequestDto(new string('a', 16001));

        var result = await _controller.ParseLog(request);

        result.Should().BeOfType<BadRequestObjectResult>();
        await _parseLogService.DidNotReceive()
            .ParseLogAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ParseLog_ReturnsServiceUnavailable_OnInvalidOperationException()
    {
        _parseLogService
            .ParseLogAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<ParsedCallLogDto>(_ => throw new InvalidOperationException("unparseable"));

        var result = await _controller.ParseLog(new ParseCallLogRequestDto("сырые заметки"));

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(503);
    }

    [Test]
    public async Task ParseLog_ReturnsServiceUnavailable_OnHttpRequestException()
    {
        _parseLogService
            .ParseLogAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<ParsedCallLogDto>(_ => throw new HttpRequestException("provider error"));

        var result = await _controller.ParseLog(new ParseCallLogRequestDto("сырые заметки"));

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(503);
    }
}
