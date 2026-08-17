namespace Sellevate.Learning.Features.Programs.Models;

/// <summary>Phase 40.17. One learner's pin, as the admin list returns it.</summary>
public record ProgramEnrollmentDto(
    Guid UserId,
    Guid ProgramVersionId,
    int ProgramVersionNumber,
    Guid? PreviousProgramVersionId,
    DateTime EnrolledAt,
    DateTime? SwitchedAt
);
