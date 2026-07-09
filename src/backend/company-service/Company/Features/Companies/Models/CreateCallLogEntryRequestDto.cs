using System.ComponentModel.DataAnnotations;

namespace Sellevate.Company.Features.Companies.Models;

public sealed record CreateCallLogEntryRequestDto(
    [Required][MaxLength(200)] string ContactName,
    [Required][MaxLength(4000)] string Subject,
    [Required][MaxLength(4000)] string Outcome,
    DateTime OccurredAt);
