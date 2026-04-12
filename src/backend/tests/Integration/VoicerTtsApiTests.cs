using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using NUnit.Framework;

namespace SalesTrainer.Tests.Integration;

/// <summary>
/// Integration tests for VoicerTTS API connectivity.
/// These tests verify that all API endpoints work correctly with the real API.
///
/// To run: dotnet test --filter "Category=VoicerTtsApi"
/// API key is read from appsettings.Testing.json (VoicerTts:ApiKey)
/// </summary>
[TestFixture]
[Category("VoicerTtsApi")]
public class VoicerTtsApiTests
{
    private HttpClient _httpClient = null!;
    private string _apiKey = null!;
    private string _baseUrl = null!;
    private string _voiceId = null!;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    [SetUp]
    public void SetUp()
    {
        // Read from appsettings.Testing.json
        _apiKey = "1367636999:414861684761733453564337307175764574354c4f673d3d";
        _baseUrl = "https://voiceapi.csv666.ru";
        _voiceId = "21m00Tcm4TlvDq8ikWAM";

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient?.Dispose();
    }

    [Test]
    public async Task CreateTask_WithValidText_ReturnsTaskId()
    {
        // Arrange - text must be at least 500 characters
        var text = new string('A', 500) + " This is a test of the VoicerTTS API.";

        var request = new
        {
            text,
            template = new
            {
                voice_id = _voiceId,
                public_owner_id = "default",
                model_id = "eleven_multilingual_v2",
                voice_settings = new
                {
                    stability = 0.5,
                    similarity_boost = 0.75,
                    use_speaker_boost = true,
                    speed = 1.0
                }
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request, JsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _httpClient.PostAsync($"{_baseUrl}/tasks", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            because: await response.Content.ReadAsStringAsync());

        var responseBody = await response.Content.ReadFromJsonAsync<JsonElement>();
        responseBody.TryGetProperty("task_id", out var taskIdProp).Should().BeTrue();
        taskIdProp.GetInt32().Should().BeGreaterThan(0);

        TestContext.WriteLine($"Created task ID: {taskIdProp.GetInt32()}");
    }

    [Test]
    public async Task CreateTask_WithTextUnder500Chars_Returns422()
    {
        // Arrange - intentionally short text
        var text = "Short text";

        var request = new
        {
            text,
            template = new
            {
                voice_id = _voiceId,
                public_owner_id = "default",
                model_id = "eleven_multilingual_v2",
                voice_settings = new
                {
                    stability = 0.5,
                    similarity_boost = 0.75,
                    use_speaker_boost = true,
                    speed = 1.0
                }
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request, JsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _httpClient.PostAsync($"{_baseUrl}/tasks", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var responseBody = await response.Content.ReadAsStringAsync();
        responseBody.Should().Contain("500");

        TestContext.WriteLine($"Response: {responseBody}");
    }

    [Test]
    public async Task CreateTask_WithInvalidApiKey_Returns401()
    {
        // Arrange
        var badClient = new HttpClient();
        badClient.DefaultRequestHeaders.Add("X-API-Key", "invalid-api-key");

        var text = new string('A', 500);
        var request = new
        {
            text,
            template = new
            {
                voice_id = _voiceId,
                public_owner_id = "default",
                model_id = "eleven_multilingual_v2"
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request, JsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await badClient.PostAsync($"{_baseUrl}/tasks", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        badClient.Dispose();
    }

    [Test]
    public async Task GetTaskStatus_WithExistingTask_ReturnsStatus()
    {
        // Arrange - create a task first
        var text = new string('А', 500) + " Это тест для проверки голосового API.";

        var createRequest = new
        {
            text,
            template = new
            {
                voice_id = _voiceId,
                public_owner_id = "default",
                model_id = "eleven_multilingual_v2",
                voice_settings = new
                {
                    stability = 0.5,
                    similarity_boost = 0.75,
                    use_speaker_boost = true,
                    speed = 1.0
                }
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(createRequest, JsonOptions),
            Encoding.UTF8,
            "application/json");

        var createResponse = await _httpClient.PostAsync($"{_baseUrl}/tasks", content);
        createResponse.EnsureSuccessStatusCode();

        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var taskId = createBody.GetProperty("task_id").GetInt32();

        // Act
        var statusResponse = await _httpClient.GetAsync($"{_baseUrl}/tasks/{taskId}/status");

        // Assert
        statusResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var statusBody = await statusResponse.Content.ReadFromJsonAsync<JsonElement>();
        statusBody.TryGetProperty("status", out var status).Should().BeTrue();

        var statusValue = status.GetString();
        statusValue.Should().BeOneOf("waiting", "processing", "ending", "ending_processed", "error");

        TestContext.WriteLine($"Task {taskId} status: {statusValue}");
    }

    [Test]
    public async Task GetTaskStatus_WithNonExistentTask_Returns404()
    {
        // Act
        var response = await _httpClient.GetAsync($"{_baseUrl}/tasks/999999999/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task FullWorkflow_CreatePollDownload_ReturnsAudio()
    {
        // Arrange
        var text = new string('А', 450) + " Привет! Это полный тест голосового синтеза. Проверяем работу API.";

        var createRequest = new
        {
            text,
            template = new
            {
                voice_id = _voiceId,
                public_owner_id = "default",
                model_id = "eleven_multilingual_v2",
                voice_settings = new
                {
                    stability = 0.5,
                    similarity_boost = 0.75,
                    use_speaker_boost = true,
                    speed = 1.0
                }
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(createRequest, JsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act - Step 1: Create task
        var createResponse = await _httpClient.PostAsync($"{_baseUrl}/tasks", content);
        createResponse.EnsureSuccessStatusCode();

        var createBody = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var taskId = createBody.GetProperty("task_id").GetInt32();
        TestContext.WriteLine($"Created task: {taskId}");

        // Act - Step 2: Poll for completion
        string? finalStatus = null;
        for (var i = 0; i < 120; i++)
        {
            await Task.Delay(500);

            var statusResponse = await _httpClient.GetAsync($"{_baseUrl}/tasks/{taskId}/status");
            statusResponse.EnsureSuccessStatusCode();

            var statusBody = await statusResponse.Content.ReadFromJsonAsync<JsonElement>();
            var status = statusBody.GetProperty("status").GetString();

            TestContext.WriteLine($"Attempt {i + 1}: status = {status}");

            if (status is "ending" or "ending_processed")
            {
                finalStatus = status;
                break;
            }

            if (status is "error" or "error_handled")
            {
                var statusLabel = statusBody.TryGetProperty("status_label", out var label)
                    ? label.GetString()
                    : "unknown";
                Assert.Fail($"Task failed with error: {statusLabel}");
            }
        }

        finalStatus.Should().NotBeNull("Task should complete within 60 seconds");

        // Act - Step 3: Download result
        var downloadResponse = await _httpClient.GetAsync($"{_baseUrl}/tasks/{taskId}/result");

        // Assert
        downloadResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        downloadResponse.Content.Headers.ContentType?.MediaType.Should().Contain("audio");

        var audioBytes = await downloadResponse.Content.ReadAsByteArrayAsync();
        audioBytes.Length.Should().BeGreaterThan(1000, "Audio file should have meaningful content");

        TestContext.WriteLine($"Downloaded audio: {audioBytes.Length} bytes");
    }

    [Test]
    public async Task GetTaskResult_WithNonExistentTask_Returns404()
    {
        // Act
        var response = await _httpClient.GetAsync($"{_baseUrl}/tasks/999999999/result");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task CreateTask_WithDifferentVoiceSettings_Works()
    {
        // Arrange - test with different stability/speed settings
        var text = new string('Б', 500);

        var request = new
        {
            text,
            template = new
            {
                voice_id = _voiceId,
                public_owner_id = "default",
                model_id = "eleven_multilingual_v2",
                voice_settings = new
                {
                    stability = 0.3,
                    similarity_boost = 0.9,
                    use_speaker_boost = false,
                    speed = 1.2
                }
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request, JsonOptions),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _httpClient.PostAsync($"{_baseUrl}/tasks", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var responseBody = await response.Content.ReadFromJsonAsync<JsonElement>();
        responseBody.GetProperty("task_id").GetInt32().Should().BeGreaterThan(0);
    }
}
