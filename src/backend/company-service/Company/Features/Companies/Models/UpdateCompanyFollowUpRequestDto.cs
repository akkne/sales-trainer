using System.ComponentModel.DataAnnotations;

namespace Sellevate.Company.Features.Companies.Models;

/// <summary>
/// Sets or clears the scheduled follow-up on a company. A non-null <see cref="NextActionAt"/>
/// (re)schedules the follow-up and resets <see cref="Company.FollowUpNotifiedAt"/> so a
/// rescheduled due date notifies again; a null <see cref="NextActionAt"/> clears the follow-up
/// entirely (note and notified-at are cleared with it).
/// </summary>
public sealed record UpdateCompanyFollowUpRequestDto(
    DateTime? NextActionAt,
    [property: MaxLength(2000)] string? NextActionNote);
