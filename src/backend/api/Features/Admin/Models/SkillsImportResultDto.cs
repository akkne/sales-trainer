namespace SalesTrainer.Api.Features.Admin;

public record SkillsImportResultDto(int SkillsCreated, int SkillsUpdated, List<string> Errors);
