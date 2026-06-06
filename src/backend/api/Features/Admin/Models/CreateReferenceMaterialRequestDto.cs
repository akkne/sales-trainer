namespace SalesTrainer.Api.Features.Admin;

public record CreateReferenceMaterialRequestDto(
    string Title,
    string MarkdownContent,
    int SortOrder,
    string? Category = null,
    string? Tags = null
);
