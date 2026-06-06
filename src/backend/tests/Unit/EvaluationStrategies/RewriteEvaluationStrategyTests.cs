using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;
using SalesTrainer.Api.Features.Exercises.Services.Implementation;
using SalesTrainer.Api.Infrastructure.Configuration;
using SalesTrainer.Api.Infrastructure.Data;

namespace SalesTrainer.Tests.Unit.EvaluationStrategies;

[TestFixture]
public class RewriteEvaluationStrategyTests
{
    private IHttpClientFactory _httpClientFactory = null!;
    private IOptions<OpenAiConfiguration> _openAiOptions = null!;
    private AppDbContext _dbContext = null!;
    private RewriteEvaluationStrategy _strategy = null!;

    [SetUp]
    public void SetUp()
    {
        _httpClientFactory = Substitute.For<IHttpClientFactory>();

        _openAiOptions = Options.Create(new OpenAiConfiguration
        {
            ApiKey = "test-api-key",
            BaseUrl = "https://api.test.com",
            ChatCompletionsPath = "/v1/chat/completions",
            OpenQuestionModel = "gpt-4"
        });

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AppDbContext(options);

        _strategy = new RewriteEvaluationStrategy(_httpClientFactory, _openAiOptions, _dbContext);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Dispose();
    }

    private static JsonElement BuildContent(
        string instruction,
        string original,
        string[]? evaluationCriteria = null,
        string? aiPrompt = null)
    {
        var obj = new Dictionary<string, object?>
        {
            ["instruction"] = instruction,
            ["original"] = original
        };
        if (evaluationCriteria != null)
            obj["evaluation_criteria"] = evaluationCriteria;
        if (aiPrompt != null)
            obj["ai_prompt"] = aiPrompt;

        return JsonDocument.Parse(JsonSerializer.Serialize(obj)).RootElement;
    }

    private static JsonElement BuildAnswer(string rewrittenText)
    {
        return JsonDocument.Parse(
            JsonSerializer.Serialize(new { rewrittenText })).RootElement;
    }

    [Test]
    public void SupportedExerciseType_ReturnsRewrite()
    {
        _strategy.SupportedExerciseType.Should().Be("rewrite");
    }

    [Test]
    public async Task EvaluateAnswerAsync_WithValidAiResponse_ReturnsCorrectResult()
    {
        var aiResponse = JsonSerializer.Serialize(new
        {
            passed = true,
            rating = 9,
            feedback = "Отличная работа! Текст стал более убедительным."
        });

        var mockHandler = new MockHttpMessageHandler(
            HttpStatusCode.OK,
            BuildF5aiResponse(aiResponse));

        var httpClient = new HttpClient(mockHandler);
        _httpClientFactory.CreateClient("OpenAI").Returns(httpClient);

        var content = BuildContent(
            instruction: "Перепишите фразу более убедительно",
            original: "Может быть, вам подойдёт наш продукт?",
            evaluationCriteria: new[] { "Уверенность", "Конкретика" });

        var answer = BuildAnswer("Наш продукт решит вашу проблему с эффективностью на 30%.");

        var result = await _strategy.EvaluateAnswerAsync(content, answer);

        result.IsCorrect.Should().BeTrue();
        result.Score.Should().Be(90);
        result.AiFeedback.Should().Be("Отличная работа! Текст стал более убедительным.");
    }

    [Test]
    public async Task EvaluateAnswerAsync_WithLowRating_ReturnsIsCorrectFalse()
    {
        var aiResponse = JsonSerializer.Serialize(new
        {
            passed = false,
            rating = 4,
            feedback = "Текст не стал лучше оригинала."
        });

        var mockHandler = new MockHttpMessageHandler(
            HttpStatusCode.OK,
            BuildF5aiResponse(aiResponse));

        var httpClient = new HttpClient(mockHandler);
        _httpClientFactory.CreateClient("OpenAI").Returns(httpClient);

        var content = BuildContent(
            instruction: "Перепишите фразу",
            original: "Может быть, вам подойдёт?");

        var answer = BuildAnswer("Может, вам это подойдёт?");

        var result = await _strategy.EvaluateAnswerAsync(content, answer);

        result.IsCorrect.Should().BeFalse();
        result.Score.Should().Be(40);
    }

    [Test]
    public async Task EvaluateAnswerAsync_WhenApiKeyNotConfigured_ThrowsException()
    {
        var unconfiguredOptions = Options.Create(new OpenAiConfiguration
        {
            ApiKey = "REPLACE_WITH_KEY"
        });

        var strategy = new RewriteEvaluationStrategy(_httpClientFactory, unconfiguredOptions, _dbContext);

        var content = BuildContent("Instruction", "Original");
        var answer = BuildAnswer("Rewritten");

        var act = async () => await strategy.EvaluateAnswerAsync(content, answer);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not configured*");
    }

    [Test]
    public async Task EvaluateAnswerAsync_WhenApiFails_ThrowsHttpRequestException()
    {
        var mockHandler = new MockHttpMessageHandler(
            HttpStatusCode.InternalServerError,
            "{\"error\": \"Internal Server Error\"}");

        var httpClient = new HttpClient(mockHandler);
        _httpClientFactory.CreateClient("OpenAI").Returns(httpClient);

        var content = BuildContent("Instruction", "Original");
        var answer = BuildAnswer("Rewritten");

        var act = async () => await _strategy.EvaluateAnswerAsync(content, answer);

        await act.Should().ThrowAsync<HttpRequestException>()
            .WithMessage("*InternalServerError*");
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

    // f5ai returns different format
    private static string BuildF5aiResponse(string content)
    {
        return JsonSerializer.Serialize(new
        {
            message = new { role = "assistant", content }
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
