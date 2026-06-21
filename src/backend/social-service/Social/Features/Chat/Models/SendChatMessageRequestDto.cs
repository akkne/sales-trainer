using System.ComponentModel.DataAnnotations;

namespace Sellevate.Social.Features.Chat.Models;

public sealed record SendChatMessageRequestDto(
    [property: Required]
    [property: MaxLength(4000)]
    string Content
);
