using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesTrainer.Api.Features.Voice.Models;
using SalesTrainer.Api.Features.Voice.Services.Abstract;

namespace SalesTrainer.Api.Features.Voice;

[ApiController]
[Route("dialog/voice")]
[Authorize]
public class VoiceConfigController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IVoicerTtsService _voicerTtsService;

    public VoiceConfigController(
        IConfiguration configuration,
        IVoicerTtsService voicerTtsService)
    {
        _configuration = configuration;
        _voicerTtsService = voicerTtsService;
    }

    [HttpGet("config")]
    public ActionResult<VoiceConfigDto> GetVoiceConfig()
    {
        var voiceEnabled = _configuration.GetValue("Voice:Enabled", false);
        var vadSilenceMs = _configuration.GetValue("Voice:VadSilenceMs", 600);
        var maxRecordingSeconds = _configuration.GetValue("Voice:MaxRecordingSeconds", 60);
        var dailyLimitMinutes = _configuration.GetValue("Voice:DailyLimitMinutes", 30);
        var monthlyLimitMinutes = _configuration.GetValue("Voice:MonthlyLimitMinutes", 300);

        // Voice is enabled if TTS is configured (STT uses browser's Web Speech API)
        var isEnabled = voiceEnabled && _voicerTtsService.IsConfigured;

        return Ok(new VoiceConfigDto
        {
            Enabled = isEnabled,
            VadSilenceMs = vadSilenceMs,
            MaxRecordingSeconds = maxRecordingSeconds,
            DailyLimitMinutes = dailyLimitMinutes,
            MonthlyLimitMinutes = monthlyLimitMinutes,
        });
    }
}
