using System.ComponentModel.DataAnnotations;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Sellevate.BuildingBlocks.Email.Abstract;
using Sellevate.BuildingBlocks.Email.Models;
using Sellevate.BuildingBlocks.Tenancy;
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

        // A single explicit transaction covers the pending-invite read as well as the write: a bare
        // SELECT has no implicit transaction for TenantConnectionInterceptor's SET LOCAL to attach
        // to, so without it row-level security would see no organization at all and return nothing
        // (docs/TENANCY/TENANCY.md §1.5).
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
        // The organization is established from the HMAC signature of the token, before any database
        // access — see InviteTokenFactory. This is the one path that cannot take it from the
        // X-Organization-Id header, because the caller has no organization yet.
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
                // The invite token itself proves control of the mailbox, so there is no separate
                // email-verification step any more (docs/EMAIL_VERIFICATION.md).
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
           ?? throw new InvalidOperationException("Organization context is not set.");

    private static OrgRole ParseRole(string role)
        => Enum.TryParse<OrgRole>(role, ignoreCase: true, out var parsedRole)
            ? parsedRole
            : throw new ArgumentException($"Unknown organization role: {role}", nameof(role));

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
