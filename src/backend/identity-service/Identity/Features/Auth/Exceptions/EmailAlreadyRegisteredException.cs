namespace Sellevate.Identity.Features.Auth.Exceptions;

/// <summary>
/// Thrown when registration targets an address that already has an account, either because the
/// pre-check found one or because the insert lost the race and hit the unique index. Sign-up is the
/// one place an honest duplicate answer is unavoidable: a form that refuses to say the address is
/// taken cannot create the account either.
///
/// This is a distinct type rather than a bare <see cref="InvalidOperationException"/> so that
/// <c>GlobalExceptionHandler</c> can answer 400 for exactly this case. The handler used to map every
/// <see cref="InvalidOperationException"/> to 400, which also caught the one Entity Framework raises
/// when the database is unreachable ("An exception has been raised that is likely due to a transient
/// failure") — so an outage was reported to the browser as a client error instead of a 5xx.
/// </summary>
public sealed class EmailAlreadyRegisteredException(string email)
    : Exception("Email already registered.")
{
    public string Email { get; } = email;
}
