namespace SalesTrainer.Api.Features.Reference;

public record ReferenceMaterialDto(
    Guid MaterialId,
    string Title,
    string MarkdownContent,
    int SortOrder
);
