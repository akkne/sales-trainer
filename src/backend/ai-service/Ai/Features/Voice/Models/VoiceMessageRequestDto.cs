using System.ComponentModel.DataAnnotations;

namespace Sellevate.Ai.Features.Voice.Models;

public sealed class VoiceMessageRequestDto
{
    [Required]
    [MaxLength(4000)]
    public string Transcript { get; set; } = null!;
}
