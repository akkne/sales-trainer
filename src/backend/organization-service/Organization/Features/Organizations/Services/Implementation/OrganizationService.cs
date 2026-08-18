using System.Text;
using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Eventing;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.Organization.Eventing;
using Sellevate.Organization.Features.Organizations.Exceptions;
using Sellevate.Organization.Features.Organizations.Models;
using Sellevate.Organization.Features.Organizations.Services.Abstract;
using Sellevate.Organization.Infrastructure.Data;
using OrganizationEntity = Sellevate.Organization.Features.Organizations.Models.Organization;

namespace Sellevate.Organization.Features.Organizations.Services.Implementation;

internal sealed class OrganizationService(OrganizationDbContext databaseContext, IEventPublisher eventPublisher)
    : IOrganizationService
{
    public async Task<OrganizationDetailDto> CreateOrganizationAsync(
        CreateOrganizationRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        var slug = await ResolveAvailableSlugAsync(request.Slug, request.Name, excludingOrganizationId: null, cancellationToken);

        var now = DateTime.UtcNow;
        var organization = new OrganizationEntity
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Slug = slug,
            Status = OrganizationStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
        };

        databaseContext.Organizations.Add(organization);
        await databaseContext.SaveChangesAsync(cancellationToken);

        await eventPublisher.PublishAsync(
            Topics.OrganizationCreated,
            organization.Id.ToString(),
            Topics.OrganizationCreated,
            new OrganizationCreatedEvent(organization.Id, organization.Name, organization.Slug),
            cancellationToken: cancellationToken);

        return ToDetailDto(organization);
    }

    public async Task<IReadOnlyList<OrganizationSummaryDto>> ListOrganizationsAsync(CancellationToken cancellationToken = default)
    {
        var organizations = await databaseContext.Organizations
            .AsNoTracking()
            .OrderByDescending(organization => organization.CreatedAt)
            .ToListAsync(cancellationToken);

        return organizations.Select(ToSummaryDto).ToList();
    }

    public async Task<OrganizationDetailDto?> GetOrganizationAsync(
        Guid organizationId, CancellationToken cancellationToken = default)
    {
        var organization = await FindOrganizationAsync(organizationId, tracking: false, cancellationToken);
        return organization is null ? null : ToDetailDto(organization);
    }

    public async Task<OrganizationDetailDto?> UpdateOrganizationAsync(
        Guid organizationId, UpdateOrganizationRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        var organization = await FindOrganizationAsync(organizationId, tracking: true, cancellationToken);
        if (organization is null)
        {
            return null;
        }

        var slug = await ResolveAvailableSlugAsync(request.Slug, request.Name, excludingOrganizationId: organizationId, cancellationToken);

        organization.Name = request.Name.Trim();
        organization.Slug = slug;
        organization.UpdatedAt = DateTime.UtcNow;
        await databaseContext.SaveChangesAsync(cancellationToken);

        await PublishUpdatedAsync(organization, cancellationToken);

        return ToDetailDto(organization);
    }

    public async Task<OrganizationDetailDto?> SuspendOrganizationAsync(
        Guid organizationId, CancellationToken cancellationToken = default)
    {
        var organization = await FindOrganizationAsync(organizationId, tracking: true, cancellationToken);
        if (organization is null)
        {
            return null;
        }

        organization.Status = OrganizationStatus.Suspended;
        organization.UpdatedAt = DateTime.UtcNow;
        await databaseContext.SaveChangesAsync(cancellationToken);

        await eventPublisher.PublishAsync(
            Topics.OrganizationSuspended,
            organization.Id.ToString(),
            Topics.OrganizationSuspended,
            new OrganizationSuspendedEvent(organization.Id, organization.Name),
            cancellationToken: cancellationToken);

        return ToDetailDto(organization);
    }

    public async Task<OrganizationDetailDto?> ReactivateOrganizationAsync(
        Guid organizationId, CancellationToken cancellationToken = default)
    {
        var organization = await FindOrganizationAsync(organizationId, tracking: true, cancellationToken);
        if (organization is null)
        {
            return null;
        }

        organization.Status = OrganizationStatus.Active;
        organization.UpdatedAt = DateTime.UtcNow;
        await databaseContext.SaveChangesAsync(cancellationToken);

        await PublishUpdatedAsync(organization, cancellationToken);

        return ToDetailDto(organization);
    }

    private Task<OrganizationEntity?> FindOrganizationAsync(Guid organizationId, bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? databaseContext.Organizations : databaseContext.Organizations.AsNoTracking();
        return query.FirstOrDefaultAsync(candidate => candidate.Id == organizationId, cancellationToken);
    }

    private Task PublishUpdatedAsync(OrganizationEntity organization, CancellationToken cancellationToken)
        => eventPublisher.PublishAsync(
            Topics.OrganizationUpdated,
            organization.Id.ToString(),
            Topics.OrganizationUpdated,
            new OrganizationUpdatedEvent(organization.Id, organization.Name, organization.Slug, organization.Status.ToString()),
            cancellationToken: cancellationToken);

    private async Task<string> ResolveAvailableSlugAsync(
        string? requestedSlug, string fallbackSource, Guid? excludingOrganizationId, CancellationToken cancellationToken)
    {
        var slug = NormalizeSlug(string.IsNullOrWhiteSpace(requestedSlug) ? fallbackSource : requestedSlug);

        var slugIsTaken = await databaseContext.Organizations
            .AsNoTracking()
            .AnyAsync(
                candidate => candidate.Slug == slug
                    && (excludingOrganizationId == null || candidate.Id != excludingOrganizationId),
                cancellationToken);

        if (slugIsTaken)
        {
            throw new OrganizationSlugConflictException(slug);
        }

        return slug;
    }

    private static string NormalizeSlug(string value)
    {
        var lowered = value.Trim().ToLowerInvariant();
        var slugBuilder = new StringBuilder(lowered.Length);
        var previousWasDash = false;

        foreach (var character in lowered)
        {
            if (char.IsLetterOrDigit(character))
            {
                slugBuilder.Append(character);
                previousWasDash = false;
            }
            else if (!previousWasDash && slugBuilder.Length > 0)
            {
                slugBuilder.Append('-');
                previousWasDash = true;
            }
        }

        var slug = slugBuilder.ToString().TrimEnd('-');
        return string.IsNullOrEmpty(slug) ? Guid.NewGuid().ToString("N") : slug;
    }

    private static OrganizationSummaryDto ToSummaryDto(OrganizationEntity organization) => new(
        organization.Id,
        organization.Name,
        organization.Slug,
        organization.Status.ToString(),
        organization.CreatedAt);

    private static OrganizationDetailDto ToDetailDto(OrganizationEntity organization) => new(
        organization.Id,
        organization.Name,
        organization.Slug,
        organization.Status.ToString(),
        organization.CreatedAt,
        organization.UpdatedAt);
}
