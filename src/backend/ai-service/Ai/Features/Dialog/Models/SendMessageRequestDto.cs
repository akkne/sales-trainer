using System.ComponentModel.DataAnnotations;

namespace Sellevate.Ai.Features.Dialog.Models;

public sealed class SendMessageRequestDto
{
    [Required]
    [MaxLength(4000)]
    public string Content { get; set; } = null!;
}
