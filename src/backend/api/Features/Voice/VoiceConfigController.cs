using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SalesTrainer.Api.Features.Voice;

[ApiController]
[Route("dialog/voice")]
[Authorize]
public class VoiceConfigController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IElevenLabsService _elevenLabsService;

    private const string PlaceholderDeepgramKey = "REPLACE_WITH_DEEPGRAM_API_KEY";

    public VoiceConfigController(
        IConfiguration configuration,
        IElevenLabsService elevenLabsService)
    {
        _configuration = configuration;
        _elevenLabsService = elevenLabsService;
    }

    [HttpGet("config")]
    public ActionResult<VoiceConfigDto> GetVoiceConfig()
    {
        var voiceEnabled = _configuration.GetValue("Voice:Enabled", false);
        var vadSilenceMs = _configuration.GetValue("Voice:VadSilenceMs", 600);
        var maxRecordingSeconds = _configuration.GetValue("Voice:MaxRecordingSeconds", 60);
        var dailyLimitMinutes = _configuration.GetValue("Voice:DailyLimitMinutes", 30);
        var monthlyLimitMinutes = _configuration.GetValue("Voice:MonthlyLimitMinutes", 300);

        var deepgramApiKey = _configuration["Deepgram:ApiKey"];
        var deepgramConfigured = !string.IsNullOrWhiteSpace(deepgramApiKey) &&
                                  deepgramApiKey != PlaceholderDeepgramKey &&
                                  !deepgramApiKey.StartsWith("REPLACE", StringComparison.OrdinalIgnoreCase);

        var isEnabled = voiceEnabled && deepgramConfigured && _elevenLabsService.IsConfigured;

        return Ok(new VoiceConfigDto
        {
            Enabled = isEnabled,
            VadSilenceMs = vadSilenceMs,
            MaxRecordingSeconds = maxRecordingSeconds,
            DailyLimitMinutes = dailyLimitMinutes,
            MonthlyLimitMinutes = monthlyLimitMinutes,
            Deepgram = new DeepgramConfigDto
            {
                Configured = deepgramConfigured,
                Model = _configuration["Deepgram:Model"] ?? "nova-3",
                Language = _configuration["Deepgram:Language"] ?? "ru",
                SmartFormat = _configuration.GetValue("Deepgram:SmartFormat", true),
                Punctuate = _configuration.GetValue("Deepgram:Punctuate", true)
            }
        });
    }

    [HttpGet("deepgram-key")]
    public ActionResult<object> GetDeepgramKey()
    {
        var deepgramApiKey = _configuration["Deepgram:ApiKey"];
        var deepgramConfigured = !string.IsNullOrWhiteSpace(deepgramApiKey) &&
                                  deepgramApiKey != PlaceholderDeepgramKey &&
                                  !deepgramApiKey.StartsWith("REPLACE", StringComparison.OrdinalIgnoreCase);

        if (!deepgramConfigured)
        {
            return StatusCode(503, new { error = "Deepgram is not configured" });
        }

        // Return key for frontend WebSocket connection
        return Ok(new { apiKey = deepgramApiKey });
    }
}
