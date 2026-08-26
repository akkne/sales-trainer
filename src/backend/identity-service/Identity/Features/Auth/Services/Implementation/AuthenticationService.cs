using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Sellevate.Identity.Common.Constants;
using Sellevate.Identity.Eventing;
using Sellevate.Identity.Features.Auth.Constants;
using Sellevate.Identity.Features.Avatars;
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
    IUserEventPublisher userEventPublisher,
    IOptions<JwtConfiguration> jwtOptions,
    IOptions<EmailVerificationConfiguration> emailVerificationOptions,
    ILogger<AuthenticationService> logger) : IAuthenticationService
{
    private bool IsEmailVerificationRequired => emailVerificationOptions.Value.Enabled;

    /// <summary>
    /// One message for "no such address", "wrong password" and "this organization does not sign in
    /// with a password at all" — see <see cref="AuthResult"/>.
    /// </summary>
    private const string LoginRejectedMessage = "Invalid email or password.";

    /// <summary>
    /// The one case Google sign-in still refuses since 40.37: the address already exists locally as
    /// an <em>unverified</em> row, so it can be neither claimed nor duplicated. Worded without
    /// naming the address's status — that row belongs to someone else by assumption.
    /// </summary>
    private const string GoogleLoginRejectedMessage =
        "This Google account cannot be used to sign in.";

    /// <summary>
    /// Public sign-up, reopened in Phase 40.37 (docs/TENANCY/TENANCY.md §4.1a) after 40.7 had
    /// deleted it.
    ///
    /// <para>
    /// The account is created with **no membership**, and nothing here creates one — that is what
    /// makes reopening the route safe rather than a rollback of 40.7's tenancy boundary. Registering
    /// buys an identity and nothing else: with no active membership the JWT carries no
    /// <c>org_id</c>, every tenant-scoped query therefore sees no organization, and the client shows
    /// the "waiting for an invitation" screen. Joining a company is still exclusively the invite's
    /// job (<c>POST /auth/invites/{token}/accept</c>), and offboarding is still membership
    /// deactivation.
    /// </para>
    ///
    /// <para>
    /// Whether the address has to be proven is <c>EmailVerification:Enabled</c>'s call, and it is
    /// off today. With it off the address is marked verified on the spot and no code is sent:
    /// verification exists to stop someone claiming a mailbox that is not theirs, and here the claim
    /// is worth nothing until an organization admin invites that exact address — at which point
    /// acceptance proves ownership anyway. It would also break local dev, where MailerSend is
    /// unconfigured and codes only reach the log (docs/EMAIL_VERIFICATION.md). With it on the row is
    /// created unverified, a code goes out, and no session is issued until
    /// <c>POST /auth/verify-email</c> consumes it.
    /// </para>
    ///
    /// <para>
    /// Duplicates are caught twice: by a read first, for the ordinary case and a clear message, and
    /// by the unique index on <c>Users.Email</c> for two requests racing on the same address.
    /// </para>
    /// </summary>
    public async Task<RegistrationResult> RegisterWithEmailAsync(
        string email,
        string password,
        string displayName,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.ToLowerInvariant();
        var requiresEmailVerification = IsEmailVerificationRequired;
        logger.LogInformation("Registration attempt {Email}", normalizedEmail);

        var existingUser = await databaseContext.Users
            .FirstOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);

        if (existingUser is not null)
        {
            logger.LogWarning("Registration failed — email already registered {Email}", normalizedEmail);
            throw new EmailAlreadyRegisteredException(normalizedEmail);
        }

        var newUserId = Guid.NewGuid();
        var newUser = new User
        {
            Id = newUserId,
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            DisplayName = displayName.Trim(),
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = !requiresEmailVerification,
            DefaultAvatarIndex = DefaultAvatarIndexResolver.Resolve(
                newUserId, DefaultAvatarSeeder.DefaultAvatarCount)
        };

        databaseContext.Users.Add(newUser);

        await userEventPublisher.PublishRegisteredAsync(
            new UserRegisteredEvent(newUser.Id, newUser.Email, newUser.DisplayName, newUser.AvatarKey),
            cancellationToken);

        try
        {
            await databaseContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            logger.LogWarning(
                "Registration failed — email already registered (unique violation) {Email}", normalizedEmail);
            throw new EmailAlreadyRegisteredException(normalizedEmail);
        }

        logger.LogInformation(
            "User registered without a membership {Email} UserId={UserId} RequiresEmailVerification={Requires}",
            normalizedEmail, newUser.Id, requiresEmailVerification);

        if (requiresEmailVerification)
        {
            // Sent after the row is committed: a code for an address that failed to save would be
            // unusable, and VerifyEmailAsync looks the user up before it looks the code up.
            await emailVerificationService.GenerateAndSendCodeAsync(
                normalizedEmail, newUser.DisplayName, cancellationToken);
            return new RegistrationResult(TokenPair: null, RequiresEmailVerification: true, normalizedEmail);
        }

        var issuedTokenPair = await IssueTokensForUserAsync(
            newUser, isOnboardingCompleted: false, cancellationToken);

        return new RegistrationResult(issuedTokenPair, RequiresEmailVerification: false, normalizedEmail);
    }

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

        // The other half of EmailVerification:Enabled. Named separately from the rejections above
        // because it is one: the credential was correct, so saying "invalid email or password" would
        // send the owner of the account round in circles instead of to the code they were mailed.
        if (IsEmailVerificationRequired && !user.IsEmailVerified)
        {
            logger.LogWarning("Login blocked — email not verified {Email} UserId={UserId}", normalizedEmail, user.Id);
            throw new EmailNotVerifiedException(normalizedEmail);
        }

        var isOnboardingCompleted = await databaseContext.UserProfiles
            .AnyAsync(profile => profile.UserId == user.Id && profile.IsOnboardingCompleted, cancellationToken);

        logger.LogInformation("Login successful {Email} UserId={UserId} Role={Role}", normalizedEmail, user.Id, user.Role);
        return await IssueTokensForUserAsync(user, isOnboardingCompleted, cancellationToken);
    }

    /// <summary>
    /// Google sign-in, which since Phase 40.37 is also a registration channel again — "регистрация
    /// через email или Google" in docs/RAW.md. An unknown Google identity is provisioned rather than
    /// rejected, exactly as <see cref="RegisterWithEmailAsync"/> provisions an unknown address, and
    /// on the same terms: a user, no membership, no organization.
    ///
    /// <para>
    /// 40.7 rejected the unknown identity because <c>POST /auth/register</c> was gone and this would
    /// have been the last self-service way in. That reasoning expired with the route's return. What
    /// it protected — that access to a customer's data comes only from an invite — is not weakened
    /// here, because neither path grants a membership.
    /// </para>
    ///
    /// <para>
    /// The membership check that used to sit after the lookup is gone too. It refused an ordinary
    /// user with no active membership on the grounds that there was nothing to sign in to; there now
    /// is — the screen that says an invitation is pending. Keeping it would have let such a user in
    /// by password and locked them out by Google, which is the same asymmetry the platform-staff
    /// carve-out existed to patch. A deactivated member likewise lands on that screen instead of a
    /// dead-end 401.
    /// </para>
    ///
    /// <para>
    /// Matching is by <c>GoogleId</c> first and only falls back to an email match when the local
    /// account is itself email-verified, so an unverified local row cannot be claimed by whoever owns
    /// the same address at Google. That row is the sole remaining rejection: it cannot be signed into
    /// and it cannot be duplicated either, since <c>Users.Email</c> is unique.
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

        var normalizedEmail = googlePayload.Email.ToLowerInvariant();

        var existingUser = await databaseContext.Users
            .FirstOrDefaultAsync(user => user.GoogleId == googlePayload.Subject, cancellationToken);

        if (existingUser is null)
        {
            var emailMatchedUser = await databaseContext.Users
                .FirstOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);

            if (emailMatchedUser is not null && !emailMatchedUser.IsEmailVerified)
            {
                logger.LogWarning(
                    "Google login rejected — address held by an unverified local account {Email}", normalizedEmail);
                throw new UnauthorizedAccessException(GoogleLoginRejectedMessage);
            }

            existingUser = emailMatchedUser ?? await ProvisionGoogleUserAsync(
                googlePayload, normalizedEmail, cancellationToken);
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
    /// Creates the account behind a first-time Google sign-in (40.37). Google has already proven the
    /// address, so the row is verified on creation and carries no password hash — the only way into
    /// it stays Google, until the owner is invited somewhere and sets one.
    ///
    /// <para>
    /// Saved here rather than by the caller so that the caller's "link Google to an existing row"
    /// branch sees a user that is already persisted, and so the unique index on the address is what
    /// settles two first sign-ins racing.
    /// </para>
    /// </summary>
    private async Task<User> ProvisionGoogleUserAsync(
        GoogleUserPayload googlePayload,
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        var newUserId = Guid.NewGuid();
        var newUser = new User
        {
            Id = newUserId,
            Email = normalizedEmail,
            GoogleId = googlePayload.Subject,
            DisplayName = string.IsNullOrWhiteSpace(googlePayload.Name)
                ? normalizedEmail
                : googlePayload.Name.Trim(),
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = true,
            DefaultAvatarIndex = DefaultAvatarIndexResolver.Resolve(
                newUserId, DefaultAvatarSeeder.DefaultAvatarCount)
        };

        databaseContext.Users.Add(newUser);

        await userEventPublisher.PublishRegisteredAsync(
            new UserRegisteredEvent(newUser.Id, newUser.Email, newUser.DisplayName, newUser.AvatarKey),
            cancellationToken);

        await databaseContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Registered a new user from Google without a membership {Email} UserId={UserId}",
            normalizedEmail, newUser.Id);

        return newUser;
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
