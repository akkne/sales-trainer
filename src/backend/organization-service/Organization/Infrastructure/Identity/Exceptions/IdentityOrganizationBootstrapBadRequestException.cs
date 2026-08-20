namespace Sellevate.Organization.Infrastructure.Identity.Exceptions;

/// <summary>
/// <c>identity-service</c> rejected the bootstrap-admin call outright — an unrecognized or
/// out-of-range <c>role</c>, or an <c>adminEmail</c> it could not accept an invite for. Carries the
/// message identity-service gave, because that message is what names which of the two it was.
/// </summary>
public sealed class IdentityOrganizationBootstrapBadRequestException(string message) : Exception(message);
