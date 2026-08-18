using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Gamification.Features.League.Models;

namespace Sellevate.Gamification.Infrastructure.Data;

/// <summary>
/// The organization leads the indexes here too, but note that the old
/// <c>UNIQUE(UserId, LeagueId)</c> was already safe: a league id belongs to exactly one organization,
/// so the pair could never span two. This is alignment, not a fix.
/// </summary>
public sealed class LeagueMembershipEntityConfiguration : IEntityTypeConfiguration<LeagueMembership>
{
    public void Configure(EntityTypeBuilder<LeagueMembership> builder)
    {
        builder.ToTable("LeagueMemberships");
        builder.HasKey(membership => membership.Id);
        builder.HasIndex(membership => new { membership.OrganizationId, membership.LeagueId });

        builder.HasIndex(membership =>
            new { membership.OrganizationId, membership.UserId, membership.LeagueId }).IsUnique();
    }
}
