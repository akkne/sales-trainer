namespace Sellevate.Learning.Features.Assignments.Models;

/// <summary>
/// Phase 40.23. The organization's roster could not be read, so who the assignment is for is
/// currently unknown.
///
/// <para>
/// <b>Its own type because its own status code.</b> Everything else that can go wrong while issuing
/// an assignment is either the caller's mistake (400) or the row's state (409); this one is neither
/// — nothing is wrong with the request and nothing is wrong with the assignment, identity-service is
/// simply not answering. It surfaces as a 503 so the РОП is told to press the button again rather
/// than being handed a 500 that reads like a bug in their assignment, and so nothing anywhere
/// mistakes "we could not find out who works here" for "nobody works here".
/// </para>
/// </summary>
public sealed class AssignmentAudienceUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
