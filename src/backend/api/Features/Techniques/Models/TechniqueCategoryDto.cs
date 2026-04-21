namespace SalesTrainer.Api.Features.Techniques.Models;

public sealed record TechniqueCategoryDto(
    string Slug,
    string Label,
    string Color,
    int SortOrder
);
