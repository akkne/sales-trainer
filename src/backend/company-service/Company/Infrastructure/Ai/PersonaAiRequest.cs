namespace Sellevate.Company.Infrastructure.Ai;

public sealed record PersonaAiRequest(string CompanyDescription, string? ContactName, string? ContactPosition, string Difficulty);
