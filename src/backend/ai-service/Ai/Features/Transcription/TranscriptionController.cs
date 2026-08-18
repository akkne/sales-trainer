using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Sellevate.Ai.Common.Constants;
using Sellevate.Ai.Features.Transcription.Constants;
using Sellevate.Ai.Features.Transcription.Models;
using Sellevate.Ai.Features.Transcription.Services.Abstract;
using Sellevate.Ai.Infrastructure.Configuration;

namespace Sellevate.Ai.Features.Transcription;

/// <summary>
/// Speech-to-text for an uploaded audio file.
///
/// <para>
/// Two size limits guard this endpoint and both are needed.
/// <see cref="WhisperConfiguration.RequestSizeLimitBytesDefault"/> is Kestrel's hard ceiling, applied
/// before any code here runs and expressed as an attribute because attributes cannot read
/// configuration; <c>Whisper:MaximumFileSizeMegabytes</c> is the smaller, configurable limit the
/// caller is actually told about. Keeping the outer one larger is what turns an oversized upload into
/// a readable message instead of a dropped connection.
/// </para>
///
/// <para>
/// A cancelled upload answers 499 rather than 500: the client going away is not a failure of this
/// service, and filing it as one buries real provider faults in the same bucket.
/// </para>
/// </summary>
[ApiController]
[Route("transcription")]
[Authorize]
public sealed class TranscriptionController(
    ITranscriptionService transcriptionService,
    IOptions<WhisperConfiguration> whisperOptions,
    ILogger<TranscriptionController> logger) : ControllerBase
{
    [HttpPost("transcribe")]
    [RequestSizeLimit(WhisperConfiguration.RequestSizeLimitBytesDefault)]
    public async Task<ActionResult<TranscriptionResponseDto>> Transcribe(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = TranscriptionMessages.FileMissingOrEmpty });

        var configuration = whisperOptions.Value;

        if (file.Length > configuration.MaximumFileSizeBytes)
            return BadRequest(new { error = TranscriptionMessages.FileTooLarge(configuration.MaximumFileSizeMegabytes) });

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!TranscriptionAudioFormats.MimeTypesByExtension.ContainsKey(extension))
            return BadRequest(new
            {
                error = TranscriptionMessages.UnsupportedFormat(
                    extension, TranscriptionAudioFormats.AcceptedExtensions),
            });

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await transcriptionService.TranscribeAsync(stream, file.FileName, cancellationToken);

            logger.LogInformation("Transcribed file {FileName} for user {UserId}",
                file.FileName, User.FindFirst("sub")?.Value);

            return Ok(new TranscriptionResponseDto(result.Text, result.Language));
        }
        catch (OperationCanceledException)
        {
            return StatusCode(
                NonStandardHttpStatusCodes.ClientClosedRequest,
                new { error = TranscriptionMessages.CancelledByClient });
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Whisper API call failed for file {FileName}", file.FileName);
            return StatusCode(
                StatusCodes.Status502BadGateway,
                new { error = TranscriptionMessages.ProviderFailed });
        }
    }
}
