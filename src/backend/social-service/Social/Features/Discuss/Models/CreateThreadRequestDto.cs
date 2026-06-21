using System.ComponentModel.DataAnnotations;

namespace Sellevate.Social.Features.Discuss.Models;

public sealed class CreateThreadRequestDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = "";

    [Required]
    [MaxLength(20000)]
    public string Body { get; set; } = "";

    public List<string> Tags { get; set; } = [];
}
