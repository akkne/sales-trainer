using System.ComponentModel.DataAnnotations;

namespace Sellevate.Social.Features.Discuss.Models;

public sealed class CreateReplyRequestDto
{
    [Required]
    [MaxLength(20000)]
    public string Body { get; set; } = "";
}
