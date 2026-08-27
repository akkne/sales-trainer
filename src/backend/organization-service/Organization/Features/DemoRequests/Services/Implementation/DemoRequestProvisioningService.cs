using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.Organization.Eventing;
using Sellevate.Organization.Features.DemoRequests.Exceptions;
using Sellevate.Organization.Features.DemoRequests.Models;
using Sellevate.Organization.Features.DemoRequests.Services.Abstract;
using Sellevate.Organization.Features.Organizations.Exceptions;
using Sellevate.Organization.Features.Organizations.Models;
using Sellevate.Organization.Features.Organizations.Services.Implementation;
using Sellevate.Organization.Infrastructure.Data;
using Sellevate.Organization.Infrastructure.Identity;
using Sellevate.Organization.Infrastructure.Identity.Exceptions;
using DemoRequestEntity = Sellevate.Organization.Features.DemoRequests.Models.DemoRequest;
using OrganizationEntity = Sellevate.Organization.Features.Organizations.Models.Organization;

namespace Sellevate.Organization.Features.DemoRequests.Services.Implementation;

/// <summary>
/// Implements the write order docs/DEMO_REQUEST.md and docs/DECISIONS.md describe for
/// <c>POST /admin/demo-requests/{id}/provision</c>. Every step below is deliberately in this order,
/// because reordering any of them reopens the failure mode the order exists to close.
///
/// <para>
/// <b>The row lock is the only thing making the null-check safe.</b> Two concurrent calls for the same
/// lead both read <see cref="DemoRequestEntity.OrganizationId"/> as <see langword="null"/> unless one
/// of them is made to wait — <c>SELECT … FOR UPDATE</c> inside the first transaction is that wait. The
/// partial unique index on <c>DemoRequests.OrganizationId</c> is the second, database-level line of
/// defense for the same property, kept even though the lock should make it unreachable in practice.
/// </para>
///
/// <para>
/// <b><c>organization.created</c> is published exactly once</b> — after the transaction that actually
/// inserts the organization row commits, never on an already-provisioned short-circuit and never again
/// on a retry that finds <see cref="DemoRequestProvisioningState.OrganizationCreated"/> already set.
/// </para>
///
/// <para>
/// <b>The call to identity-service sits outside every transaction.</b> Holding a database transaction
/// open across a network call would let a slow or hung identity-service pin a Postgres connection and
/// a row lock for however long the HTTP timeout allows — the organization row is already committed by
/// the time this call happens, so there is nothing left that needs the transaction's isolation.
/// </para>
/// </summary>
internal sealed class DemoRequestProvisioningService(
    OrganizationDbContext databaseContext,
    IIdentityOrganizationBootstrapClient identityOrganizationBootstrapClient,
    IEventPublisher eventPublisher,
    ILogger<DemoRequestProvisioningService> logger) : IDemoRequestProvisioningService
{
    public async Task<DemoRequestProvisioningResultDto?> ProvisionAsync(
        Guid demoRequestId,
        ProvisionDemoRequestRequestDto request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (demoRequest, justCreatedOrganization, alreadyProvisionedResult) =
            await LockAndAdvanceToOrganizationCreatedAsync(demoRequestId, request, cancellationToken);

        if (demoRequest is null)
        {
            return null;
        }

        if (alreadyProvisionedResult is not null)
        {
            return alreadyProvisionedResult;
        }

        if (justCreatedOrganization is not null)
        {
            await eventPublisher.PublishAsync(
                Topics.OrganizationCreated,
                justCreatedOrganization.Id.ToString(),
                Topics.OrganizationCreated,
                new OrganizationCreatedEvent(
                    justCreatedOrganization.Id, justCreatedOrganization.Name, justCreatedOrganization.Slug),
                cancellationToken: cancellationToken);
        }

        var organizationReference = justCreatedOrganization is not null
            ? new ProvisionedOrganizationDto(justCreatedOrganization.Id, justCreatedOrganization.Name, justCreatedOrganization.Slug)
            : await LoadOrganizationReferenceAsync(demoRequest.OrganizationId!.Value, cancellationToken);

        var inviteResult = await BootstrapAdministratorAsync(demoRequest, organizationReference, request, actorUserId, cancellationToken);

        await RecordAdminInvitedAsync(demoRequest, inviteResult.InviteId, cancellationToken);

        return new DemoRequestProvisioningResultDto(
            demoRequest.Id,
            demoRequest.Status.ToString(),
            nameof(DemoRequestProvisioningState.AdminInvited),
            organizationReference,
            inviteResult.InviteId,
            inviteResult.Email,
            inviteResult.ExpiresAt,
            AlreadyProvisioned: false);
    }

    /// <summary>
    /// Steps 1–3 of the write order in one transaction: lock the row, answer the already-provisioned
    /// fast path with no side effects, or resolve the slug and insert the organization alongside the
    /// lead's own advance to <see cref="DemoRequestProvisioningState.OrganizationCreated"/> — one
    /// <c>SaveChangesAsync</c>, one commit, both rows together. A slug conflict rolls the whole
    /// transaction back before either row is written, so there is nothing to clean up.
    /// </summary>
    private async Task<(DemoRequestEntity? DemoRequest, OrganizationEntity? JustCreatedOrganization, DemoRequestProvisioningResultDto? AlreadyProvisioned)>
        LockAndAdvanceToOrganizationCreatedAsync(
            Guid demoRequestId, ProvisionDemoRequestRequestDto request, CancellationToken cancellationToken)
    {
        await using var transaction = await databaseContext.Database.BeginTransactionAsync(cancellationToken);

        var demoRequest = await LockDemoRequestAsync(demoRequestId, cancellationToken);
        if (demoRequest is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return (null, null, null);
        }

        if (demoRequest.ProvisioningState == DemoRequestProvisioningState.AdminInvited)
        {
            var alreadyProvisioned = await BuildAlreadyProvisionedResultAsync(demoRequest, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return (demoRequest, null, alreadyProvisioned);
        }

        if (demoRequest.OrganizationId is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return (demoRequest, null, null);
        }

        var organizationName = string.IsNullOrWhiteSpace(request.OrganizationName)
            ? demoRequest.CompanyName
            : request.OrganizationName.Trim();

        string slug;
        try
        {
            slug = await OrganizationService.ResolveAvailableSlugAsync(
                databaseContext, request.Slug, organizationName, excludingOrganizationId: null, cancellationToken);
        }
        catch (OrganizationSlugConflictException)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        var now = DateTime.UtcNow;
        var organization = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            Name = organizationName,
            Slug = slug,
            Status = OrganizationStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };

        databaseContext.Organizations.Add(organization);

        demoRequest.OrganizationId = organization.Id;
        demoRequest.ProvisioningState = DemoRequestProvisioningState.OrganizationCreated;
        demoRequest.Status = DemoRequestStatus.Approved;
        demoRequest.BootstrapAdminEmail = ResolveAdminEmail(demoRequest, request);
        demoRequest.UpdatedAt = now;

        await databaseContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return (demoRequest, organization, null);
    }

    /// <summary>
    /// <c>SELECT … FOR UPDATE</c> only has an effect on a relational provider — the in-memory provider
    /// the unit tests run against has no row locking at all, so it is skipped there the same way every
    /// other raw-SQL call in this codebase guards itself (see <c>AiSpendMeter.AddToLedgerAsync</c>).
    /// </summary>
    private async Task<DemoRequestEntity?> LockDemoRequestAsync(Guid demoRequestId, CancellationToken cancellationToken)
    {
        if (databaseContext.Database.IsRelational())
        {
            await databaseContext.Database.ExecuteSqlInterpolatedAsync(
                $"""SELECT "Id" FROM "DemoRequests" WHERE "Id" = {demoRequestId} FOR UPDATE""",
                cancellationToken);
        }

        return await databaseContext.DemoRequests
            .FirstOrDefaultAsync(candidate => candidate.Id == demoRequestId, cancellationToken);
    }

    /// <summary>
    /// Step 5 of the write order, deliberately outside any database transaction — see the class
    /// summary. Maps identity-service's distinguishable outcomes onto the exceptions the controller
    /// knows how to render, and everything else onto
    /// <see cref="DemoRequestInviteFailedException"/> so the lead always stays retryable rather than
    /// surfacing a 500. An organization that already has administrators is not one of those outcomes:
    /// it may have as many as it needs, so provisioning a second one is an ordinary success.
    /// </summary>
    private async Task<IdentityBootstrapAdminResult> BootstrapAdministratorAsync(
        DemoRequestEntity demoRequest,
        ProvisionedOrganizationDto organizationReference,
        ProvisionDemoRequestRequestDto request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await identityOrganizationBootstrapClient.BootstrapAdministratorAsync(
                organizationReference.Id,
                organizationReference.Name,
                organizationReference.Slug,
                demoRequest.BootstrapAdminEmail!,
                request.Role,
                actorUserId,
                cancellationToken);
        }
        catch (IdentityOrganizationBootstrapBadRequestException badRequestException)
        {
            throw new ArgumentException(badRequestException.Message);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Bootstrap-admin call to identity-service failed for demo request {DemoRequestId}, organization {OrganizationId}; "
                + "lead stays at OrganizationCreated for retry",
                demoRequest.Id, organizationReference.Id);
            throw new DemoRequestInviteFailedException(organizationReference.Id);
        }
    }

    /// <summary>Step 6 of the write order: its own transaction, opened only after identity-service has
    /// actually answered.</summary>
    private async Task RecordAdminInvitedAsync(DemoRequestEntity demoRequest, Guid inviteId, CancellationToken cancellationToken)
    {
        await using var transaction = await databaseContext.Database.BeginTransactionAsync(cancellationToken);

        var now = DateTime.UtcNow;
        demoRequest.BootstrapInviteId = inviteId;
        demoRequest.ProvisioningState = DemoRequestProvisioningState.AdminInvited;
        demoRequest.ProvisionedAt = now;
        demoRequest.UpdatedAt = now;

        await databaseContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<ProvisionedOrganizationDto> LoadOrganizationReferenceAsync(
        Guid organizationId, CancellationToken cancellationToken)
    {
        var organization = await databaseContext.Organizations
            .AsNoTracking()
            .FirstAsync(candidate => candidate.Id == organizationId, cancellationToken);

        return new ProvisionedOrganizationDto(organization.Id, organization.Name, organization.Slug);
    }

    private async Task<DemoRequestProvisioningResultDto> BuildAlreadyProvisionedResultAsync(
        DemoRequestEntity demoRequest, CancellationToken cancellationToken)
    {
        var organizationReference = await LoadOrganizationReferenceAsync(demoRequest.OrganizationId!.Value, cancellationToken);

        return new DemoRequestProvisioningResultDto(
            demoRequest.Id,
            demoRequest.Status.ToString(),
            nameof(DemoRequestProvisioningState.AdminInvited),
            organizationReference,
            demoRequest.BootstrapInviteId!.Value,
            demoRequest.BootstrapAdminEmail!,
            InviteExpiresAt: null,
            AlreadyProvisioned: true);
    }

    /// <summary>
    /// Resolved and stored exactly once, at organization-creation time — see the field comment on
    /// <see cref="DemoRequestEntity.BootstrapAdminEmail"/> for why a later call never overrides it.
    /// </summary>
    private static string ResolveAdminEmail(DemoRequestEntity demoRequest, ProvisionDemoRequestRequestDto request)
        => string.IsNullOrWhiteSpace(request.AdminEmail)
            ? demoRequest.WorkEmail
            : request.AdminEmail.Trim().ToLowerInvariant();
}
