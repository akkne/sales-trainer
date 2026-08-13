using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NUnit.Framework;
using Sellevate.Ai.Features.Dialog;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Features.Dialog.Services.Abstract;
using Sellevate.Ai.Infrastructure.Data;

namespace Sellevate.Ai.Tests.Unit;

/// <summary>
/// A rejected or unreachable LLM must degrade into a meaningful status code. Before this,
/// a provider 400 escaped <see cref="DialogController"/> as an unhandled exception, which
/// the pipeline turned into a 500 and logged as a service defect.
/// </summary>
[TestFixture]
public class DialogControllerProviderFailureTests
{
    private IDialogService _dialogService = null!;
    private AiDbContext _databaseContext = null!;
    private DialogController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _dialogService = Substitute.For<IDialogService>();
        _dialogService.IsOpenAiConfigured.Returns(true);

        _databaseContext = new AiDbContext(new DbContextOptionsBuilder<AiDbContext>()
            .UseInMemoryDatabase("dialog-provider-failure-" + Guid.NewGuid())
            .Options);

        _controller = new DialogController(_dialogService, _databaseContext, NullLogger<DialogController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "test"))
                }
            }
        };
    }

    [TearDown]
    public void TearDown() => _databaseContext.Dispose();

    private void FailSendMessageWith(Exception exception) =>
        _dialogService
            .SendMessageAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<DialogMessage>(_ => throw exception);

    private Task<IActionResult> SendMessageAsync() =>
        _controller.SendMessage("session-1", new SendMessageRequestDto { Content = "привет" });

    [Test]
    public async Task SendMessage_ReturnsBadGateway_WhenProviderRejectsTheRequest()
    {
        FailSendMessageWith(new OpenAiRequestException("AI provider error", 400));

        var result = await SendMessageAsync();

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(502);
    }

    [Test]
    public async Task SendMessage_ReturnsServiceUnavailable_WhenProviderIsUnreachable()
    {
        FailSendMessageWith(new HttpRequestException("connection refused"));

        var result = await SendMessageAsync();

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(503);
    }

    [Test]
    public async Task SendMessage_ReturnsTooManyRequests_WhenProviderRateLimits()
    {
        FailSendMessageWith(new OpenAiRateLimitException("slow down"));

        var result = await SendMessageAsync();

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(429);
    }

    [Test]
    public async Task CompleteSession_ReturnsBadGateway_WhenFeedbackGenerationIsRejected()
    {
        _dialogService
            .CompleteSessionAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns<DialogFeedbackResult?>(_ => throw new OpenAiRequestException("AI provider error", 400));

        var result = await _controller.CompleteSession("session-1");

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(502);
    }

    [Test]
    public async Task StartSession_ReturnsBadGateway_WhenProviderRejectsTheRequest()
    {
        _dialogService
            .StartSessionAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(),
                Arg.Any<CompanyCallContext?>(), Arg.Any<CancellationToken>())
            .Returns<DialogSession>(_ => throw new OpenAiRequestException("AI provider error", 400));

        var result = await _controller.StartSession(new StartSessionRequestDto
        {
            BundleId = Guid.NewGuid(),
            ModeId = Guid.NewGuid()
        });

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(502);
    }
}
