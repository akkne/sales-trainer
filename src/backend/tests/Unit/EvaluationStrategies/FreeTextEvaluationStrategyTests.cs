using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using NUnit.Framework;
using SalesTrainer.Api.Features.Exercises.Services.Implementation;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Tests.Unit.EvaluationStrategies;

[TestFixture]
public class FreeTextEvaluationStrategyTests
{
    private IHttpClientFactory _httpClientFactory = null!;
    private IConfiguration _configuration = null!;
    private AppDbContext _dbContext = null!;
    private FreeTextEvaluationStrategy _strategy = null!;

    [SetUp]
    public void SetUp()
    {
        _httpClientFactory = Substitute.For<IHttpClientFactory>();

        var configValues = new Dictionary<string, string?>
        {
            ["OpenAI:ApiKey"] = "test-api-key",
            ["OpenAI:BaseUrl"] = "https://api.test.com",
            ["OpenAI:ChatCompletionsPath"] = "/v1/chat/completions",
            ["OpenAI:OpenQuestionModel"] = "gpt-4",
            ["OpenAI:MaxTokensOpenQuestion"] = "300"
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);

        _strategy = new FreeTextEvaluationStrategy(_httpClientFactory, _configuration, _dbContext);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Dispose();
    }

    private static JsonElement BuildContent(
        string instruction,
        string[]? expectedKeywords = null,
        string? aiPrompt = null)
    {
        var obj = new Dictionary<string, object?>
        {
            ["instruction"] = instruction
        };
        if (expectedKeywords != null)
            obj["expected_keywords"] = expectedKeywords;
        if (aiPrompt != null)
            obj["ai_prompt"] = aiPrompt;

        return JsonDocument.Parse(JsonSerializer.Serialize(obj)).RootElement;
    }

    private static JsonElement BuildAnswer(string text)
    {
        return JsonDocument.Parse(
            JsonSerializer.Serialize(new { text })).RootElement;
    }

    [Test]
    public void SupportedExerciseType_ReturnsFreeText()
    {
        _strategy.SupportedExerciseType.Should().Be("free_text");
    }

    [Test]
    public async Task EvaluateAnswerAsync_WithHighRating_ReturnsCorrect()
    {
        var aiResponse = JsonSerializer.Serialize(new
        {
            passed = true,
            rating = 8,
            feedback = "Хороший ответ с использованием ключевых понятий."
        });

        var mockHandler = new MockHttpMessageHandler(
            HttpStatusCode.OK,
            BuildOpenAiResponse(aiResponse));

        var httpClient = new HttpClient(mockHandler);
        _httpClientFactory.CreateClient("OpenAI").Returns(httpClient);

        var content = BuildContent(
            instruction: "Объясните принцип активного слушания",
            expectedKeywords: new[] { "внимание", "перефразирование", "уточнение" });

        var answer = BuildAnswer("Активное слушание предполагает полное внимание к собеседнику, перефразирование его слов и уточняющие вопросы.");

        var result = await _strategy.EvaluateAnswerAsync(content, answer);

        result.IsCorrect.Should().BeTrue();
        result.Score.Should().Be(80);
        result.AiFeedback.Should().Contain("Хороший ответ");
    }

    [Test]
    public async Task EvaluateAnswerAsync_WithMediumRating_ReturnsIncorrect()
    {
        var aiResponse = JsonSerializer.Serialize(new
        {
            passed = false,
            rating = 5,
            feedback = "Ответ неполный."
        });

        var mockHandler = new MockHttpMessageHandler(
            HttpStatusCode.OK,
            BuildOpenAiResponse(aiResponse));

        var httpClient = new HttpClient(mockHandler);
        _httpClientFactory.CreateClient("OpenAI").Returns(httpClient);

        var content = BuildContent(instruction: "Объясните концепцию");
        var answer = BuildAnswer("Это концепция.");

        var result = await _strategy.EvaluateAnswerAsync(content, answer);

        result.IsCorrect.Should().BeFalse();
        result.Score.Should().Be(50);
    }

    private static string BuildOpenAiResponse(string content)
    {
        return JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new { content }
                }
            }
        });
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
