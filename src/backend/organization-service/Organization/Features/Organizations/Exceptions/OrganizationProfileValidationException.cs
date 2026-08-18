namespace Sellevate.Organization.Features.Organizations.Exceptions;

/// <summary>
/// Phase 40.29. The request was malformed in a way model binding cannot see. Rendered as 400 by
/// <c>OrganizationProfileController</c>, the same shape learning-service's
/// <c>ContentGenerationValidationException</c> has, so the two halves of the same flow fail alike.
/// </summary>
public sealed class OrganizationProfileValidationException(string message) : Exception(message);
