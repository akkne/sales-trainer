using System.ComponentModel.DataAnnotations;

namespace Sellevate.Company.Features.Companies.Models;

public sealed record UpdateCallLogEntryRequestDto(
    [Required][MaxLength(200)] string ContactName,
    [MaxLength(4000)] string Subject,
    [MaxLength(4000)] string Outcome,
    DateTime OccurredAt);
