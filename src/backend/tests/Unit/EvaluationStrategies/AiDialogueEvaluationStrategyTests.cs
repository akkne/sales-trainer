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
public class AiDialogueEvaluationStrategyTests
{
    private IHttpClientFactory _httpClientFactory = null!;
    private IOptions<OpenAiConfiguration> _openAiOptions = null!;
    private AppDbContext _dbContext = null!;
    private AiDialogueEvaluationStrategy _strategy = null!;

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

        _strategy = new AiDialogueEvaluationStrategy(_httpClientFactory, _openAiOptions, _dbContext);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Dispose();
    }

    private static JsonElement BuildContent(
        string persona,
        string scenario,
        string[]? successCriteria = null,
        string? aiPrompt = null,
        int maxTurns = 6)
    {
        var obj = new Dictionary<string, object?>
        {
            ["persona"] = persona,
            ["scenario"] = scenario,
            ["max_turns"] = maxTurns
        };
        if (successCriteria != null)
            obj["success_criteria"] = successCriteria;
        if (aiPrompt != null)
            obj["ai_prompt"] = aiPrompt;

        return JsonDocument.Parse(JsonSerializer.Serialize(obj)).RootElement;
    }

    private static JsonElement BuildAnswer(
        object[] messages,
        bool completedNaturally = true)
    {
        return JsonDocument.Parse(
            JsonSerializer.Serialize(new { messages, completedNaturally })).RootElement;
    }

    [Test]
    public void SupportedExerciseType_ReturnsAiDialog()
    {
        _strategy.SupportedExerciseType.Should().Be("ai_dialogue");
    }

    [Test]
    public async Task EvaluateAnswerAsync_WithSuccessfulDialogue_ReturnsHighScore()
    {
        var aiResponse = JsonSerializer.Serialize(new
        {
            passed = true,
            rating = 9,
            feedback = "Отлично провели разговор. Клиент согласился на встречу."
        });

        var mockHandler = new MockHttpMessageHandler(
            HttpStatusCode.OK,
            BuildOpenAiResponse(aiResponse));

        var httpClient = new HttpClient(mockHandler);
        _httpClientFactory.CreateClient("OpenAI").Returns(httpClient);

        var content = BuildContent(
            persona: "Секретарь Мария",
            scenario: "Холодный звонок в офис",
            successCriteria: new[] { "Назначить встречу", "Получить контакт ЛПР" },
            maxTurns: 6);

        var messages = new object[]
        {
            new { role = "assistant", content = "Компания АБВ, слушаю вас." },
            new { role = "user", content = "Добрый день! Я звоню по вопросу оптимизации закупок." },
            new { role = "assistant", content = "И что вы предлагаете?" },
            new { role = "user", content = "Мы помогаем сократить расходы на 20%. Могу я поговорить с руководителем отдела закупок?" },
            new { role = "assistant", content = "Хорошо, соединяю." },
            new { role = "user", content = "Спасибо большое!" }
        };

        var answer = BuildAnswer(messages, completedNaturally: true);

        var result = await _strategy.EvaluateAnswerAsync(content, answer);

        result.IsCorrect.Should().BeTrue();
        result.Score.Should().Be(90);
        result.AiFeedback.Should().Contain("Отлично");
    }

    [Test]
    public async Task EvaluateAnswerAsync_WithPoorDialogue_ReturnsLowScore()
    {
        var aiResponse = JsonSerializer.Serialize(new
        {
            passed = false,
            rating = 3,
            feedback = "Диалог завершился неудачно. Секретарь отказала в соединении."
        });

        var mockHandler = new MockHttpMessageHandler(
            HttpStatusCode.OK,
            BuildOpenAiResponse(aiResponse));

        var httpClient = new HttpClient(mockHandler);
        _httpClientFactory.CreateClient("OpenAI").Returns(httpClient);

        var content = BuildContent(
            persona: "Строгий секретарь",
            scenario: "Холодный звонок");

        var messages = new object[]
        {
            new { role = "assistant", content = "Да, слушаю." },
            new { role = "user", content = "Здравствуйте, э-э-э... можно директора?" },
            new { role = "assistant", content = "По какому вопросу?" },
            new { role = "user", content = "Ну... хотел предложить сотрудничество..." }
        };

        var answer = BuildAnswer(messages, completedNaturally: false);

        var result = await _strategy.EvaluateAnswerAsync(content, answer);

        result.IsCorrect.Should().BeFalse();
        result.Score.Should().Be(30);
    }

    [Test]
    public async Task EvaluateAnswerAsync_ExtractsConversationCorrectly()
    {
        var aiResponse = JsonSerializer.Serialize(new
        {
            passed = true,
            rating = 8,
            feedback = "Хороший диалог."
        });

        var mockHandler = new MockHttpMessageHandler(
            HttpStatusCode.OK,
            BuildOpenAiResponse(aiResponse));

        var httpClient = new HttpClient(mockHandler);
        _httpClientFactory.CreateClient("OpenAI").Returns(httpClient);

        var content = BuildContent(
            persona: "Клиент",
            scenario: "Презентация продукта",
            maxTurns: 4);

        var messages = new object[]
        {
            new { role = "assistant", content = "Расскажите о вашем продукте." },
            new { role = "user", content = "Наш продукт помогает экономить время." },
            new { role = "assistant", content = "Интересно, а сколько стоит?" },
            new { role = "user", content = "Цена зависит от объема." }
        };

        var answer = BuildAnswer(messages);

        var result = await _strategy.EvaluateAnswerAsync(content, answer);

        result.IsCorrect.Should().BeTrue();
        result.Score.Should().Be(80);
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
