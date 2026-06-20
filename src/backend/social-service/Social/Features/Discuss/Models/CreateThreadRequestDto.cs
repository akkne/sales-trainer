namespace Sellevate.Social.Features.Discuss.Models;

public sealed class CreateThreadRequestDto
{
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public List<string> Tags { get; set; } = [];
}
