using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Sellevate.Ai.Features.Voice.Models;
using Sellevate.Ai.Features.Voice.Services.Abstract;
using Sellevate.Ai.Infrastructure.Configuration;

namespace Sellevate.Ai.Features.Voice;

[ApiController]
[Route("dialog/voice")]
[Authorize]
public class VoiceConfigController : ControllerBase
{
    private readonly IOptions<VoiceFeatureConfiguration> _voiceFeatureOptions;
    private readonly ITtsRouter _ttsRouter;

    public VoiceConfigController(
        IOptions<VoiceFeatureConfiguration> voiceFeatureOptions,
        ITtsRouter ttsRouter)
    {
        _voiceFeatureOptions = voiceFeatureOptions;
        _ttsRouter = ttsRouter;
    }

    [HttpGet("config")]
    public ActionResult<VoiceConfigDto> GetVoiceConfiguration()
    {
        var voiceFeatureConfiguration = _voiceFeatureOptions.Value;
        var isEnabled = voiceFeatureConfiguration.Enabled && _ttsRouter.IsConfigured;

        return Ok(new VoiceConfigDto
        {
            Enabled = isEnabled,
            VadSilenceMs = voiceFeatureConfiguration.VadSilenceMilliseconds,
            MaxRecordingSeconds = voiceFeatureConfiguration.MaxRecordingSeconds,
            DailyLimitMinutes = voiceFeatureConfiguration.DailyLimitMinutes,
            MonthlyLimitMinutes = voiceFeatureConfiguration.MonthlyLimitMinutes,
        });
    }
}
