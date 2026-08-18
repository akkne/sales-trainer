using System.ComponentModel.DataAnnotations;
using Sellevate.Company.Common.Constants;

namespace Sellevate.Company.Features.Companies.Models;

public sealed record CreatePracticeCallRequestDto(
    [Required][MaxLength(CompanyFieldLengths.DialogSessionId)] string DialogSessionId,
    [MaxLength(CompanyFieldLengths.PracticeCallGoal)] string? Goal = null);
