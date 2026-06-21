using System.ComponentModel.DataAnnotations;

namespace Sellevate.Social.Features.Chat.Models;

public sealed record SendChatMessageRequestDto(
    [Required]
    [MaxLength(4000)]
    string Content
);
