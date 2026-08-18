using Microsoft.EntityFrameworkCore;
using Sellevate.Identity.Features.Auth.Constants;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Auth.Services.Abstract;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Features.Auth.Services.Implementation;

/// <summary>
/// The only <see cref="IAuthProvider"/> that exists. It holds the bcrypt check that used to sit
/// inline in <c>AuthenticationService.LoginWithEmailAsync</c>; moving it behind the interface is
/// the entire behavioural change of Phase 40.8's seam.
/// </summary>
internal sealed class PasswordAuthProvider(
    IdentityDbContext databaseContext,
    ILogger<PasswordAuthProvider> logger) : IAuthProvider
{
    public string Method => AuthMethodNames.Password;

    public async Task<AuthResult> AuthenticateAsync(
        AuthRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await databaseContext.Users
            .FirstOrDefaultAsync(candidate => candidate.Email == request.Email, cancellationToken);

        // A user created through Google sign-in has no PasswordHash; an empty password must never
        // be handed to BCrypt.Verify against a null hash.
        if (user is null
            || user.PasswordHash is null
            || string.IsNullOrEmpty(request.Password)
            || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            logger.LogWarning("Password authentication failed for {Email}", request.Email);
            return AuthResult.Failed;
        }

        return AuthResult.Succeeded(user);
    }
}
