using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using SalesTrainer.Api.Features.Dialog.Models;
using SalesTrainer.Api.Features.Dialog.Services.Implementation;
using SalesTrainer.Api.Infrastructure.Configuration;

namespace SalesTrainer.Tests.Unit;

[TestFixture]
public class OpenAiChatServiceTests
{
    private static OpenAiChatService CreateService(HttpStatusCode statusCode, string responseContent)
    {
        var openAiOptions = Options.Create(new OpenAiConfiguration
        {
            ApiKey = "test-api-key",
            BaseUrl = "https://api.openai.com"
        });

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient("OpenAI").Returns(new HttpClient(new MockHttpMessageHandler(statusCode, responseContent)));

        return new OpenAiChatService(httpClientFactory, openAiOptions, NullLogger<OpenAiChatService>.Instance);
    }

    private static string BuildCompletionResponse(string content)
    {
        return JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { message = new { role = "assistant", content } }
            }
        });
    }

    private static List<DialogMessage> BuildConversation()
    {
        return
        [
            new DialogMessage { Role = "user", Content = "Добрый день, это Иван из «Сейлз Трейнер»." },
            new DialogMessage { Role = "assistant", Content = "Слушаю вас, говорите быстрее." }
        ];
    }

    [Test]
    public async Task GenerateFeedbackAsync_WithXpTag_ParsesRewardAndBlocks()
    {
        var aiResponse = "<strong>Хорошее начало</strong>, но цель не достигнута.\n[DETAILED]\n<h3>Общая оценка</h3><p>Разбор.</p>\n[XP:55]";
        var service = CreateService(HttpStatusCode.OK, BuildCompletionResponse(aiResponse));

        var result = await service.GenerateFeedbackAsync("Оцени разговор.", BuildConversation());

        result.XpReward.Should().Be(55);
        result.Summary.Should().Contain("Хорошее начало");
        result.Content.Should().Contain("<h3>Общая оценка</h3>");
        result.Content.Should().NotContain("[XP:");
    }

    [Test]
    public async Task GenerateFeedbackAsync_WithoutXpTag_AwardsZero()
    {
        var aiResponse = "Резюме без тега.\n[DETAILED]\n<p>Разбор без оценки.</p>";
        var service = CreateService(HttpStatusCode.OK, BuildCompletionResponse(aiResponse));

        var result = await service.GenerateFeedbackAsync("Оцени разговор.", BuildConversation());

        result.XpReward.Should().Be(0);
    }

    [Test]
    public async Task GenerateFeedbackAsync_ClampsXpToHundred()
    {
        var aiResponse = "Резюме.\n[DETAILED]\n<p>Разбор.</p>\n[XP:250]";
        var service = CreateService(HttpStatusCode.OK, BuildCompletionResponse(aiResponse));

        var result = await service.GenerateFeedbackAsync("Оцени разговор.", BuildConversation());

        result.XpReward.Should().Be(100);
    }

    [Test]
    public async Task SendChatMessageAsync_ParsesStructuredReply()
    {
        var aiResponse = "{\"reply\": \"Я уже сказал нет. До свидания.\", \"endCall\": true}";
        var service = CreateService(HttpStatusCode.OK, BuildCompletionResponse(aiResponse));

        var result = await service.SendChatMessageAsync("Ты занятой директор.", BuildConversation());

        result.Content.Should().Be("Я уже сказал нет. До свидания.");
        result.IsStopSignal.Should().BeTrue();
    }

    [Test]
    public async Task SendChatMessageAsync_FallsBackToPlainTextReply()
    {
        var aiResponse = "Слушаю вас, что вы хотели?";
        var service = CreateService(HttpStatusCode.OK, BuildCompletionResponse(aiResponse));

        var result = await service.SendChatMessageAsync("Ты занятой директор.", BuildConversation());

        result.Content.Should().Be("Слушаю вас, что вы хотели?");
        result.IsStopSignal.Should().BeFalse();
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseContent;

        public MockHttpMessageHandler(HttpStatusCode statusCode, string responseContent)
        {
            _statusCode = statusCode;
            _responseContent = responseContent;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage
            {
                StatusCode = _statusCode,
                Content = new StringContent(_responseContent)
            });
        }
    }
}
