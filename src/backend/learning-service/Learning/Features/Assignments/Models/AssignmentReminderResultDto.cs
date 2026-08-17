namespace Sellevate.Learning.Features.Assignments.Models;

/// <summary>
/// Phase 40.23. What the "remind" button did.
/// </summary>
/// <param name="NotifiedCount">
/// How many people were nudged — everybody on the assignment who has not completed it, including
/// those under the threshold. Zero is a real and useful answer: it means the whole team is done.
/// </param>
public sealed record AssignmentReminderResultDto(int NotifiedCount);
