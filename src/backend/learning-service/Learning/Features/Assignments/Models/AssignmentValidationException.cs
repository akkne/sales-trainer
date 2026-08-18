namespace Sellevate.Learning.Features.Assignments.Models;

/// <summary>
/// Phase 40.21. A rejected payload: an unknown kind, a reference that is not a uuid where one is
/// required, a completion rule that is not an object, a deadline before the opening time. Distinct
/// from <c>AssignmentWriteResult</c>, which reports states of an existing row.
/// </summary>
public sealed class AssignmentValidationException(string message) : Exception(message);
