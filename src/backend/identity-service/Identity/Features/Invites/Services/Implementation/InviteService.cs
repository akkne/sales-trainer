using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Email.Abstract;
using Sellevate.BuildingBlocks.Email.Models;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Identity.Common.Constants;
using Sellevate.Identity.Eventing;
using Sellevate.Identity.Features.Auth.Models;
using Sellevate.Identity.Features.Auth.Services.Abstract;
using Sellevate.Identity.Features.Avatars;
using Sellevate.Identity.Features.Invites.Constants;
using Sellevate.Identity.Features.Invites.Exceptions;
using Sellevate.Identity.Features.Invites.Models;
using Sellevate.Identity.Features.Invites.Services.Abstract;
using Sellevate.Identity.Features.Membership.Models;
using Sellevate.Identity.Infrastructure.Configuration;
using Sellevate.Identity.Infrastructure.Data;
using MembershipEntity = Sellevate.Identity.Features.Membership.Models.Membership;

namespace Sellevate.Identity.Features.Invites.Services.Implementation;

/// <summary>
/// Owns the whole life of an invite: issue, revoke, accept. Accepting is the only way an account
/// comes into existence (docs/TENANCY/TENANCY.md §4.1), so this is also the user-provisioning path.
///
/// <para>
/// Every method wraps its reads and writes in one explicit transaction. A bare <c>SELECT</c> has no
/// implicit transaction for <c>TenantConnectionInterceptor</c>'s <c>SET LOCAL</c> to attach to, so
/// without it row-level security sees no organization at all and returns nothing
/// (docs/TENANCY/TENANCY.md §1.5).
/// </para>
///
/// <para>
/// Acceptance takes the organization from the HMAC signature of the token, before any database
/// access — see <see cref="IInviteTokenFactory"/>. It is the one path that cannot read the
/// <c>X-Organization-Id</c> header, because the caller has no organization yet. This is why
/// <see cref="TenantContext"/> is injected concretely rather than as <c>ITenantContext</c>: the
/// scope has to be pointed at the token's organization from inside the service.
/// </para>
///
/// <para>
/// A user created here is marked email-verified on the spot. The invite token itself proves control
/// of the mailbox, so there is no separate verification step any more
/// (docs/EMAIL_VERIFICATION.md).
/// </para>
/// </summary>
internal sealed class InviteService(
    IdentityDbContext databaseContext,
    TenantContext tenantContext,
    IInviteTokenFactory inviteTokenFactory,
    IAuthenticationService authenticationService,
    IUserEventPublisher userEventPublisher,
    IEmailSender emailSender,
    IOptions<InviteConfiguration> inviteOptions,
    ILogger<InviteService> logger) : IInviteService
{
    private const string LegacyOrganizationAdminRoleName = "OrgAdmin";

    private static readonly EmailAddressAttribute EmailAddressValidator = new();

    public async Task<CreateInvitesResponseDto> CreateAsync(
        CreateInvitesRequestDto request,
        Guid invitedByUserId,
        CancellationToken cancellationToken = default)
    {
        var currentOrganizationId = RequireOrganizationId();
        var role = ParseRole(request.Role);
        var configuration = inviteOptions.Value;
        var now = DateTime.UtcNow;

        var createdInvites = new List<CreatedInviteDto>();
        var rejectedInvites = new List<RejectedInviteDto>();
        var acceptedNormalizedEmails = new HashSet<string>(StringComparer.Ordinal);

        var requestedEmails = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            requestedEmails.Add(request.Email);
        }

        if (request.Emails is not null)
        {
            requestedEmails.AddRange(request.Emails);
        }

        await using var transaction = await databaseContext.Database.BeginTransactionAsync(cancellationToken);

        var pendingInviteEmails = await databaseContext.Invites
            .Where(invite => invite.AcceptedAt == null && invite.RevokedAt == null && invite.ExpiresAt > now)
            .Select(invite => invite.Email)
            .ToListAsync(cancellationToken);
        var pendingInviteEmailSet = new HashSet<string>(pendingInviteEmails, StringComparer.Ordinal);

        foreach (var requestedEmail in requestedEmails)
        {
            var normalizedEmail = (requestedEmail ?? string.Empty).Trim().ToLowerInvariant();

            if (normalizedEmail.Length == 0 || !EmailAddressValidator.IsValid(normalizedEmail))
            {
                rejectedInvites.Add(new RejectedInviteDto(requestedEmail ?? string.Empty, InviteConstants.InvalidEmailReason));
                continue;
            }

            if (!acceptedNormalizedEmails.Add(normalizedEmail))
            {
                rejectedInvites.Add(new RejectedInviteDto(normalizedEmail, InviteConstants.DuplicateInRequestReason));
                continue;
            }

            if (pendingInviteEmailSet.Contains(normalizedEmail))
            {
                rejectedInvites.Add(new RejectedInviteDto(normalizedEmail, InviteConstants.AlreadyInvitedReason));
                continue;
            }

            var isAlreadyActiveMember = await databaseContext.Users
                .Where(user => user.Email == normalizedEmail)
                .Join(
                    databaseContext.Memberships.Where(membership =>
                        membership.OrganizationId == currentOrganizationId
                        && membership.Status == MembershipStatus.Active),
                    user => user.Id,
                    membership => membership.UserId,
                    (_, membership) => membership.UserId)
                .AnyAsync(cancellationToken);

            if (isAlreadyActiveMember)
            {
                rejectedInvites.Add(new RejectedInviteDto(normalizedEmail, InviteConstants.AlreadyMemberReason));
                continue;
            }

            var issuedToken = inviteTokenFactory.Issue(currentOrganizationId);
            var invite = new Invite
            {
                Id = Guid.NewGuid(),
                OrganizationId = currentOrganizationId,
                Email = normalizedEmail,
                Role = role,
                TokenHash = issuedToken.TokenHash,
                ExpiresAt = now.AddHours(configuration.TokenLifetimeHours),
                InvitedBy = invitedByUserId,
                CreatedAt = now
            };

            databaseContext.Invites.Add(invite);
            createdInvites.Add(new CreatedInviteDto(
                invite.Id, invite.Email, invite.Role.ToString(), invite.ExpiresAt, issuedToken.RawToken));
        }

        await databaseContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        foreach (var createdInvite in createdInvites)
        {
            await emailSender.SendEmailAsync(
                BuildInviteEmail(createdInvite, configuration.AcceptUrl),
                cancellationToken);
        }

        logger.LogInformation(
            "Invites created OrganizationId={OrganizationIdentifier} Created={CreatedCount} Rejected={RejectedCount} InvitedBy={InvitedBy}",
            currentOrganizationId, createdInvites.Count, rejectedInvites.Count, invitedByUserId);

        return new CreateInvitesResponseDto(createdInvites, rejectedInvites);
    }

    public async Task<bool> RevokeAsync(Guid inviteId, CancellationToken cancellationToken = default)
    {
        var currentOrganizationId = RequireOrganizationId();

        await using var transaction = await databaseContext.Database.BeginTransactionAsync(cancellationToken);

        var invite = await databaseContext.Invites
            .FirstOrDefaultAsync(candidate => candidate.Id == inviteId, cancellationToken);

        if (invite is null || invite.AcceptedAt is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        if (invite.RevokedAt is null)
        {
            invite.RevokedAt = DateTime.UtcNow;
            await databaseContext.SaveChangesAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Invite revoked InviteId={InviteId} OrganizationId={OrganizationIdentifier}", inviteId, currentOrganizationId);
        return true;
    }

    public async Task<IssuedTokenPair> AcceptAsync(
        string rawToken,
        AcceptInviteRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (!inviteTokenFactory.TryReadOrganizationId(rawToken, out var tokenOrganizationId))
        {
            throw new InviteNotAcceptableException(InviteRejectionReason.NotFound, InviteConstants.NotFoundMessage);
        }

        if (tenantContext.OrganizationId is { } alreadyScopedOrganizationId)
        {
            if (alreadyScopedOrganizationId != tokenOrganizationId)
            {
                throw new InviteNotAcceptableException(InviteRejectionReason.NotFound, InviteConstants.NotFoundMessage);
            }
        }
        else
        {
            tenantContext.SetOrganization(tokenOrganizationId);
        }

        var tokenHash = inviteTokenFactory.ComputeTokenHash(rawToken);
        var now = DateTime.UtcNow;

        await using var transaction = await databaseContext.Database.BeginTransactionAsync(cancellationToken);

        var invite = await databaseContext.Invites
            .FirstOrDefaultAsync(candidate => candidate.TokenHash == tokenHash, cancellationToken)
            ?? throw new InviteNotAcceptableException(InviteRejectionReason.NotFound, InviteConstants.NotFoundMessage);

        if (invite.AcceptedAt is not null)
        {
            throw new InviteNotAcceptableException(
                InviteRejectionReason.AlreadyAccepted, InviteConstants.AlreadyAcceptedMessage);
        }

        if (invite.RevokedAt is not null)
        {
            throw new InviteNotAcceptableException(InviteRejectionReason.Revoked, InviteConstants.RevokedMessage);
        }

        if (invite.ExpiresAt <= now)
        {
            throw new InviteNotAcceptableException(InviteRejectionReason.Expired, InviteConstants.ExpiredMessage);
        }

        var user = await databaseContext.Users
            .FirstOrDefaultAsync(candidate => candidate.Email == invite.Email, cancellationToken);

        var isNewUser = user is null;
        if (user is null)
        {
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                throw new InviteNotAcceptableException(
                    InviteRejectionReason.PasswordRequired, InviteConstants.PasswordRequiredMessage);
            }

            var newUserId = Guid.NewGuid();
            user = new User
            {
                Id = newUserId,
                Email = invite.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? invite.Email : request.DisplayName.Trim(),
                CreatedAt = now,
                IsEmailVerified = true,
                DefaultAvatarIndex = DefaultAvatarIndexResolver.Resolve(newUserId, DefaultAvatarSeeder.DefaultAvatarCount)
            };
            databaseContext.Users.Add(user);
        }
        else if (!user.IsEmailVerified)
        {
            user.IsEmailVerified = true;
        }

        var membership = await databaseContext.Memberships
            .FirstOrDefaultAsync(
                candidate => candidate.UserId == user.Id && candidate.OrganizationId == invite.OrganizationId,
                cancellationToken);

        if (membership is null)
        {
            databaseContext.Memberships.Add(new MembershipEntity
            {
                UserId = user.Id,
                OrganizationId = invite.OrganizationId,
                Role = invite.Role,
                Status = MembershipStatus.Active,
                InvitedBy = invite.InvitedBy,
                JoinedAt = now
            });
        }
        else
        {
            membership.Role = invite.Role;
            membership.Status = MembershipStatus.Active;
            membership.DeactivatedAt = null;
        }

        invite.AcceptedAt = now;

        if (isNewUser)
        {
            await userEventPublisher.PublishRegisteredAsync(
                new UserRegisteredEvent(user.Id, user.Email, user.DisplayName, user.AvatarKey),
                cancellationToken);
        }

        await databaseContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation(
            "Invite accepted InviteId={InviteId} OrganizationId={OrganizationIdentifier} UserId={UserId} NewUser={IsNewUser}",
            invite.Id, invite.OrganizationId, user.Id, isNewUser);

        return await authenticationService.IssueTokensForUserAsync(user, cancellationToken);
    }

    private Guid RequireOrganizationId()
        => tenantContext.OrganizationId
           ?? throw new InvalidOperationException(ErrorMessages.OrganizationContextNotSet);

    /// <summary>
    /// Accepts only the names declared on <see cref="OrgRole"/>. The 2026-08-16 role split renamed
    /// <c>OrgAdmin</c> to <see cref="OrgRole.TenancyAdmin"/>; the old name is rejected rather than
    /// silently mapped, so a stale caller finds out instead of quietly inviting someone at a role
    /// it did not mean (docs/DECISIONS.md). The rejection message names the replacement, so the
    /// fix is obvious from the response alone.
    ///
    /// <para>
    /// <c>Enum.TryParse</c> also accepts bare numbers and happily produces values that are not
    /// declared at all (<c>"99"</c> would become <c>(OrgRole)99</c>), so the parse is followed by
    /// an <c>IsDefined</c> check rather than trusted on its own.
    /// </para>
    /// </summary>
    private static OrgRole ParseRole(string role)
    {
        if (string.Equals(role, LegacyOrganizationAdminRoleName, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"The organization role '{LegacyOrganizationAdminRoleName}' no longer exists — "
                + $"use '{nameof(OrgRole.TenancyAdmin)}' or '{nameof(OrgRole.TenancySuperAdmin)}'.",
                nameof(role));
        }

        return Enum.TryParse<OrgRole>(role, ignoreCase: true, out var parsedRole)
               && Enum.IsDefined(parsedRole)
            ? parsedRole
            : throw new ArgumentException($"Unknown organization role: {role}", nameof(role));
    }

    private static EmailMessage BuildInviteEmail(CreatedInviteDto invite, string acceptUrl)
    {
        var acceptLink = $"{acceptUrl.TrimEnd('/')}/{invite.Token}";
        var escapedAcceptLink = WebUtility.HtmlEncode(acceptLink);
        var textBody =
            "Здравствуйте!\n\n" +
            "Вас пригласили в Sellevate. Чтобы принять приглашение, перейдите по ссылке:\n" +
            $"{acceptLink}\n\n" +
            $"Ссылка действует до {invite.ExpiresAt:yyyy-MM-dd HH:mm} UTC и работает один раз.";
        var htmlBody =
            "<p>Здравствуйте!</p>" +
            $"<p>Вас пригласили в Sellevate. <a href=\"{escapedAcceptLink}\">Принять приглашение</a>.</p>" +
            $"<p>Ссылка действует до {invite.ExpiresAt:yyyy-MM-dd HH:mm} UTC и работает один раз.</p>";

        return new EmailMessage(
            RecipientEmail: invite.Email,
            RecipientName: invite.Email,
            Subject: InviteConstants.EmailSubject,
            HtmlBody: htmlBody,
            TextBody: textBody);
    }
}
