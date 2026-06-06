namespace SalesTrainer.Api.Features.Techniques.Models;

public sealed record TechniqueDialogTurnDto(
    int OrderIndex,
    string Side,
    string Text,
    TechniqueDialogAnnotationDto[] Annotations
);
