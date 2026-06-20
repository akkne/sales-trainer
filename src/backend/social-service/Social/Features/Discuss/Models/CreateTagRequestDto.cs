namespace Sellevate.Social.Features.Discuss.Models;

public sealed class CreateTagRequestDto
{
    public string Name { get; set; } = "";
    public string? Slug { get; set; }
}
