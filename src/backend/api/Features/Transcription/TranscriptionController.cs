using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SalesTrainer.Api.Common.Constants;
using SalesTrainer.Api.Features.Transcription.Models;
using SalesTrainer.Api.Features.Transcription.Services.Abstract;
using SalesTrainer.Api.Infrastructure.Configuration;

namespace SalesTrainer.Api.Features.Transcription;

[ApiController]
[Route("transcription")]
[Authorize]
public sealed class TranscriptionController(
    ITranscriptionService transcriptionService,
    IOptions<WhisperConfiguration> whisperOptions,
    ILogger<TranscriptionController> logger) : ControllerBase
{
    [HttpPost("transcribe")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<ActionResult<TranscriptionResponseDto>> Transcribe(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Аудиофайл не передан или пустой." });

        var maximumFileSizeMegabytes = whisperOptions.Value.MaximumFileSizeMegabytes;
        var maxBytes = maximumFileSizeMegabytes * 1024 * 1024;

        if (file.Length > maxBytes)
            return BadRequest(new { error = $"Размер файла превышает {maximumFileSizeMegabytes} МБ." });

        var allowed = new[] { ".mp3", ".mp4", ".m4a", ".mpeg", ".mpga", ".wav", ".webm", ".ogg" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext))
            return BadRequest(new { error = $"Формат {ext} не поддерживается. Допустимые: {string.Join(", ", allowed)}." });

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
            return StatusCode(NonStandardHttpStatusCodes.ClientClosedRequest, new { error = "Запрос отменён клиентом." });
        }
        catch (InvalidOperationException ex)
        {
            logger.LogError(ex, "Whisper API call failed for file {FileName}", file.FileName);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Ошибка при обращении к Whisper API.", detail = ex.Message });
        }
    }
}
