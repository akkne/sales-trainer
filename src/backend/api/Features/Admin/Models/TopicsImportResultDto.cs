namespace SalesTrainer.Api.Features.Admin;

public record TopicsImportResultDto(int TopicsCreated, int TopicsUpdated, List<string> Errors);
