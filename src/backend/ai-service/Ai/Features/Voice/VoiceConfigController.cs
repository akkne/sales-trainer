using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Sellevate.Ai.Features.Voice.Models;
using Sellevate.Ai.Features.Voice.Services.Abstract;
using Sellevate.Ai.Infrastructure.Configuration;

namespace Sellevate.Ai.Features.Voice;

/// <summary>
/// Tells the browser client whether voice is available and what limits to enforce locally.
///
/// <para>
/// <c>Enabled</c> is the feature switch ANDed with the provider actually being configured, so a
/// deployment that turned voice on without supplying a key reports it off rather than handing the client
/// a microphone that fails on the first turn. The limits are advisory: the client uses them to show a
/// countdown, and every one of them is enforced again server-side.
/// </para>
/// </summary>
[ApiController]
[Route("dialog/voice")]
[Authorize]
public sealed class VoiceConfigController : ControllerBase
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
