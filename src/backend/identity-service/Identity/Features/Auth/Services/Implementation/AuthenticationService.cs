using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Sellevate.Identity.Common.Constants;
using Sellevate.Identity.Features.Auth.Constants;
using Sellevate.Identity.Features.Auth.Exceptions;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Auth.Services.Abstract;
using Sellevate.Identity.Features.Membership.Models;
using Sellevate.Identity.Features.Organizations.Models;
using Sellevate.Identity.Infrastructure.Configuration;
using Sellevate.Identity.Infrastructure.Data;

namespace Sellevate.Identity.Features.Auth.Services.Implementation;

/// <summary>
/// Mints and revokes the platform's tokens. Every entry route — password login, Google sign-in,
/// invite acceptance, email verification, refresh — converges on this class, which is what makes it
/// the one place organization suspension can be enforced without leaving whichever route is added
/// next unguarded.
///
/// <para>
/// Email confirmation is currently disabled on the password login path: an unverified account is
/// <em>not</em> blocked from signing in. That is a temporary product decision, not an oversight — the
/// verification machinery is still live for the routes that use it.
/// </para>
/// </summary>
internal sealed class AuthenticationService(
    IdentityDbContext databaseContext,
    IEmailVerificationService emailVerificationService,
    IGoogleTokenValidator googleTokenValidator,
    IOrganizationAuthConfigurationResolver organizationAuthConfigurationResolver,
    IEnumerable<IAuthProvider> authProviders,
    IOptions<JwtConfiguration> jwtOptions,
    ILogger<AuthenticationService> logger) : IAuthenticationService
{
    /// <summary>
    /// One message for "no such address", "wrong password" and "this organization does not sign in
    /// with a password at all" — see <see cref="AuthResult"/>.
    /// </summary>
    private const string LoginRejectedMessage = "Invalid email or password.";

    /// <summary>
    /// Deliberately identical for "no such account" and "account without an active membership":
    /// a distinguishable answer would turn the Google endpoint into an oracle telling an outsider
    /// which addresses belong to a Sellevate customer.
    /// </summary>
    private const string GoogleLoginRejectedMessage =
        "This Google account has no access. Access to Sellevate is by invitation only.";

    public async Task<IssuedTokenPair> VerifyEmailAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.ToLowerInvariant();

        var user = await databaseContext.Users
            .FirstOrDefaultAsync(userRecord => userRecord.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            logger.LogWarning("Email verification failed — unknown email {Email}", normalizedEmail);
            throw new UnauthorizedAccessException(EmailVerificationConstants.InvalidCodeMessage);
        }

        var isCodeValid = await emailVerificationService.VerifyCodeAsync(normalizedEmail, code, cancellationToken);
        if (!isCodeValid)
        {
            throw new UnauthorizedAccessException(EmailVerificationConstants.InvalidCodeMessage);
        }

        if (!user.IsEmailVerified)
        {
            user.IsEmailVerified = true;
            await databaseContext.SaveChangesAsync(cancellationToken);
        }

        var isOnboardingCompleted = await databaseContext.UserProfiles
            .AnyAsync(profile => profile.UserId == user.Id && profile.IsOnboardingCompleted, cancellationToken);

        logger.LogInformation("Email verified {Email} UserId={UserId}", normalizedEmail, user.Id);
        return await IssueTokensForUserAsync(user, isOnboardingCompleted, cancellationToken);
    }

    public async Task ResendVerificationCodeAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.ToLowerInvariant();

        var user = await databaseContext.Users
            .FirstOrDefaultAsync(userRecord => userRecord.Email == normalizedEmail, cancellationToken);

        if (user is null || user.IsEmailVerified)
        {
            logger.LogInformation(
                "Resend verification code ignored for {Email} (unknown or already verified)", normalizedEmail);
            return;
        }

        await emailVerificationService.GenerateAndSendCodeAsync(normalizedEmail, user.DisplayName, cancellationToken);
    }

    public Task<ResolvedLoginMethod> ResolveLoginMethodAsync(
        string email,
        CancellationToken cancellationToken = default)
        => organizationAuthConfigurationResolver.ResolveForEmailAsync(email, cancellationToken);

    /// <summary>
    /// Step 3 of the three-step login flow (docs/TENANCY/TENANCY.md §4.5): the organization the
    /// address resolves to decides which <see cref="IAuthProvider"/> gets the credential. Today
    /// that is always <c>PasswordAuthProvider</c>; an organization configured for a method with no
    /// provider is refused rather than quietly falling back to a password, which is what makes the
    /// seam worth having before SSO exists.
    /// </summary>
    public async Task<IssuedTokenPair> LoginWithEmailAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.ToLowerInvariant();
        logger.LogInformation("Login attempt {Email}", normalizedEmail);

        var resolvedLoginMethod = await organizationAuthConfigurationResolver
            .ResolveForEmailAsync(normalizedEmail, cancellationToken);

        var authProvider = authProviders
            .FirstOrDefault(candidate => candidate.Method == resolvedLoginMethod.Method);

        if (authProvider is null)
        {
            logger.LogWarning(
                "Login failed — no provider implements method {Method} configured for OrganizationId={OrganizationIdentifier}",
                resolvedLoginMethod.Method, resolvedLoginMethod.OrganizationId);
            throw new UnauthorizedAccessException(LoginRejectedMessage);
        }

        var authResult = await authProvider.AuthenticateAsync(
            new AuthRequest(normalizedEmail, password), cancellationToken);

        if (authResult.AuthenticatedUser is not { } user)
        {
            logger.LogWarning("Login failed — invalid credentials {Email}", normalizedEmail);
            throw new UnauthorizedAccessException(LoginRejectedMessage);
        }

        var isOnboardingCompleted = await databaseContext.UserProfiles
            .AnyAsync(profile => profile.UserId == user.Id && profile.IsOnboardingCompleted, cancellationToken);

        logger.LogInformation("Login successful {Email} UserId={UserId} Role={Role}", normalizedEmail, user.Id, user.Role);
        return await IssueTokensForUserAsync(user, isOnboardingCompleted, cancellationToken);
    }

    /// <summary>
    /// Google sign-in is a login method, never a registration channel (Phase 40.7). With
    /// <c>POST /auth/register</c> deleted, an implicit "create the account on first Google login"
    /// branch would be the last remaining self-service way into the product — the exact hole
    /// docs/TENANCY/TENANCY.md §4.1 closes. An account arrives only through an invite, so an unknown
    /// Google identity is rejected rather than provisioned.
    ///
    /// <para>
    /// Matching is by <c>GoogleId</c> first and only falls back to an email match when the local
    /// account is itself email-verified, so an unverified local row cannot be claimed by whoever owns
    /// the same address at Google.
    /// </para>
    ///
    /// <para>
    /// Having an account is not enough: without an active membership an ordinary user belongs to no
    /// organization and there is nothing to sign in to. Platform staff are the exception, and
    /// deliberately so — <c>Admin</c> and <c>SuperAdmin</c> are Sellevate's own roles and are not
    /// bounded by tenancy (docs/DECISIONS.md, 2026-08-16); they normally hold no membership anywhere
    /// and their whole job lives on the platform admin routes. Without that carve-out the password
    /// path would admit them while the Google path locked them out of their own product. They still
    /// receive no <c>org_id</c>/<c>org_role</c> claim — absent membership stays absent, it is never
    /// implied.
    /// </para>
    /// </summary>
    public async Task<IssuedTokenPair> LoginWithGoogleAsync(
        string googleIdToken,
        CancellationToken cancellationToken = default)
    {
        var googlePayload = await googleTokenValidator.ValidateAsync(googleIdToken, cancellationToken);

        if (!googlePayload.IsEmailVerified)
        {
            logger.LogWarning("Google login rejected — email not verified by Google {Email}", googlePayload.Email);
            throw new UnauthorizedAccessException("Google account email is not verified.");
        }

        logger.LogInformation("Google login attempt {Email}", googlePayload.Email);

        var existingUser = await databaseContext.Users
            .FirstOrDefaultAsync(user => user.GoogleId == googlePayload.Subject, cancellationToken);

        if (existingUser is null)
        {
            var emailMatchedUser = await databaseContext.Users
                .FirstOrDefaultAsync(user => user.Email == googlePayload.Email, cancellationToken);

            if (emailMatchedUser is not null && emailMatchedUser.IsEmailVerified)
            {
                existingUser = emailMatchedUser;
            }
        }

        if (existingUser is null)
        {
            logger.LogWarning(
                "Google login rejected — no account for {Email}; access is invite-only", googlePayload.Email);
            throw new UnauthorizedAccessException(GoogleLoginRejectedMessage);
        }

        var isPlatformStaff = existingUser.Role is UserRole.Admin or UserRole.SuperAdmin;
        var hasActiveMembership = await databaseContext.Memberships
            .AnyAsync(
                candidate => candidate.UserId == existingUser.Id && candidate.Status == MembershipStatus.Active,
                cancellationToken);

        if (!hasActiveMembership && !isPlatformStaff)
        {
            logger.LogWarning(
                "Google login rejected — no active membership for {Email} UserId={UserId}",
                googlePayload.Email, existingUser.Id);
            throw new UnauthorizedAccessException(GoogleLoginRejectedMessage);
        }

        if (existingUser.GoogleId is null)
        {
            existingUser.GoogleId = googlePayload.Subject;
            existingUser.IsEmailVerified = true;
            await databaseContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Linked Google account to existing user {Email} UserId={UserId}", googlePayload.Email, existingUser.Id);
        }
        else
        {
            logger.LogInformation("Google login successful {Email} UserId={UserId}", googlePayload.Email, existingUser.Id);
        }

        var isOnboardingCompleted = await databaseContext.UserProfiles
            .AnyAsync(profile => profile.UserId == existingUser.Id && profile.IsOnboardingCompleted, cancellationToken);

        return await IssueTokensForUserAsync(existingUser, isOnboardingCompleted, cancellationToken);
    }

    /// <summary>
    /// Rotates a refresh token, and treats reuse of an already-rotated one as a compromise.
    ///
    /// <para>
    /// The stored row is loaded regardless of <c>IsRevoked</c> so that reuse can be detected at all,
    /// and revocation is then a single conditional <c>UPDATE ... WHERE NOT IsRevoked</c>: whichever
    /// concurrent caller updates zero rows is the one holding a token that was already spent. That
    /// caller revokes the whole family, on the assumption that a replayed token means someone else
    /// has a copy.
    /// </para>
    /// </summary>
    public async Task<IssuedTokenPair> RefreshAccessTokenAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = ComputeTokenHash(rawRefreshToken);

        var storedToken = await databaseContext.RefreshTokens
            .Include(token => token.User)
            .FirstOrDefaultAsync(token => token.Token == tokenHash, cancellationToken);

        if (storedToken is null || storedToken.ExpiresAt < DateTime.UtcNow)
        {
            logger.LogWarning("Token refresh failed — invalid or expired refresh token");
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");
        }

        var revokedRowCount = await databaseContext.RefreshTokens
            .Where(token => token.Id == storedToken.Id && !token.IsRevoked)
            .ExecuteUpdateAsync(
                update => update.SetProperty(token => token.IsRevoked, true),
                cancellationToken);

        if (revokedRowCount == 0)
        {
            logger.LogWarning(
                "Refresh-token reuse detected for UserId={UserId}; revoking all active refresh tokens",
                storedToken.UserId);
            await databaseContext.RefreshTokens
                .Where(token => token.UserId == storedToken.UserId && !token.IsRevoked)
                .ExecuteUpdateAsync(
                    update => update.SetProperty(token => token.IsRevoked, true),
                    cancellationToken);
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");
        }

        var isOnboardingCompleted = await databaseContext.UserProfiles
            .AnyAsync(profile => profile.UserId == storedToken.UserId && profile.IsOnboardingCompleted, cancellationToken);

        logger.LogInformation("Access token refreshed for UserId={UserId}", storedToken.UserId);
        return await IssueTokensForUserAsync(storedToken.User, isOnboardingCompleted, cancellationToken);
    }

    public async Task RevokeRefreshTokenAsync(
        string rawRefreshToken,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = ComputeTokenHash(rawRefreshToken);

        var storedToken = await databaseContext.RefreshTokens
            .FirstOrDefaultAsync(token => token.Token == tokenHash, cancellationToken);

        if (storedToken is null)
        {
            return;
        }

        storedToken.IsRevoked = true;
        await databaseContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Refresh token revoked for UserId={UserId}", storedToken.UserId);
    }

    public async Task<IssuedTokenPair> IssueTokensForUserAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        var isOnboardingCompleted = await databaseContext.UserProfiles
            .AnyAsync(profile => profile.UserId == user.Id && profile.IsOnboardingCompleted, cancellationToken);

        return await IssueTokensForUserAsync(user, isOnboardingCompleted, cancellationToken);
    }

    /// <summary>
    /// The single funnel every issued token passes through.
    ///
    /// <para>
    /// Phase 40.6: at most one active membership is expected while the UI only allows a single
    /// organization per user; ordering by <c>JoinedAt</c> keeps the choice deterministic if that ever
    /// stops being true before multi-org support lands. A user with no membership gets no
    /// <c>org_id</c>/<c>org_role</c> claim — absent membership is never implicit organization access.
    /// </para>
    ///
    /// <para>
    /// Phase 40.9: suspension is enforced here, at the one point password login, Google, invite
    /// acceptance and refresh all converge on. Doing it per-controller would leave whichever route is
    /// added next unguarded, and doing it only at login would leave already-issued refresh tokens
    /// working indefinitely.
    /// </para>
    /// </summary>
    private async Task<IssuedTokenPair> IssueTokensForUserAsync(
        User user,
        bool isOnboardingCompleted,
        CancellationToken cancellationToken = default)
    {
        var membership = await databaseContext.Memberships
            .AsNoTracking()
            .Where(candidate => candidate.UserId == user.Id && candidate.Status == MembershipStatus.Active)
            .OrderBy(candidate => candidate.JoinedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (membership is not null)
        {
            await RejectIfOrganizationIsSuspendedAsync(membership.OrganizationId, cancellationToken);
        }

        var accessToken = BuildJwtAccessToken(user, membership);
        var rawRefreshToken = GenerateSecureRandomToken();

        var newRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = ComputeTokenHash(rawRefreshToken),
            ExpiresAt = DateTime.UtcNow.AddDays(jwtOptions.Value.RefreshTokenLifetimeDays),
            IsRevoked = false
        };

        databaseContext.RefreshTokens.Add(newRefreshToken);
        await databaseContext.SaveChangesAsync(cancellationToken);

        return new IssuedTokenPair(
            AccessToken: accessToken,
            RefreshToken: rawRefreshToken,
            UserId: user.Id.ToString(),
            DisplayName: user.DisplayName,
            IsOnboardingCompleted: isOnboardingCompleted,
            Role: user.Role,
            OrgId: membership?.OrganizationId.ToString(),
            OrgRole: membership?.Role.ToString()
        );
    }

    /// <summary>
    /// An organization identity-service has never heard of is treated as active: the replica is
    /// populated asynchronously over Kafka, and a consumer that is briefly behind must not lock a
    /// paying customer out of their own product. Suspension is a deliberate, recorded platform act
    /// — its absence is what "no row" means here, not "unknown, therefore refuse".
    /// </summary>
    private async Task RejectIfOrganizationIsSuspendedAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var isSuspended = await databaseContext.OrganizationReplicas
            .AsNoTracking()
            .AnyAsync(
                replica => replica.OrganizationId == organizationId
                    && replica.Status == OrganizationReplicaStatus.Suspended,
                cancellationToken);

        if (!isSuspended)
        {
            return;
        }

        logger.LogWarning(
            "Token issuance refused — OrganizationId={OrganizationIdentifier} is suspended", organizationId);
        throw new OrganizationSuspendedException(organizationId);
    }

    private string BuildJwtAccessToken(User user, Features.Membership.Models.Membership? membership)
    {
        var signingKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtOptions.Value.Key));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypeNames.DisplayName, user.DisplayName),
            new(ClaimTypes.Role, user.Role.ToString())
        };

        if (membership is not null)
        {
            claims.Add(new Claim(ClaimTypeNames.OrganizationId, membership.OrganizationId.ToString()));
            claims.Add(new Claim(
                AuthorizationPolicies.OrganizationRoleClaimType, membership.Role.ToString()));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(jwtOptions.Value.AccessTokenLifetimeMinutes),
            Issuer = jwtOptions.Value.Issuer,
            Audience = jwtOptions.Value.Audience,
            SigningCredentials = new SigningCredentials(
                signingKey, SecurityAlgorithms.HmacSha256)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var securityToken = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(securityToken);
    }

    private static string GenerateSecureRandomToken()
    {
        var randomBytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(randomBytes);
    }

    private static string ComputeTokenHash(string rawToken)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexString(hashBytes);
    }
}
