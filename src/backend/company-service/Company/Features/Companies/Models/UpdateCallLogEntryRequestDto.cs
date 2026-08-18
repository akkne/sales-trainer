using System.ComponentModel.DataAnnotations;
using Sellevate.Company.Common.Constants;

namespace Sellevate.Company.Features.Companies.Models;

public sealed record UpdateCallLogEntryRequestDto(
    [Required][MaxLength(CompanyFieldLengths.Name)] string ContactName,
    [MaxLength(CompanyFieldLengths.CallLogSubject)] string Subject,
    [MaxLength(CompanyFieldLengths.CallLogOutcome)] string Outcome,
    DateTime OccurredAt,
    Guid? ContactId = null);
