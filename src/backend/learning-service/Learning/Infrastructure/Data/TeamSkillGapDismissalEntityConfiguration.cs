using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.TeamInsights.Models;

namespace Sellevate.Learning.Infrastructure.Data;

/// <summary>
/// Maps one РОП's «не сейчас» about one skill-gap suggestion.
///
/// <para>
/// <b>One live refusal per stage per organization, and the database is what says so.</b> Two tabs
/// pressing «не сейчас» on the same red cell collide on this index rather than leaving two rows that
/// expire on different days — the same use of a unique index 40.24 made for repeat waves. Tenant-leading,
/// like every other index on a strict tenant table: the query filter and the RLS policy both put the
/// organization in front of the predicate.
/// </para>
/// </summary>
public sealed class TeamSkillGapDismissalEntityConfiguration : IEntityTypeConfiguration<TeamSkillGapDismissal>
{
    public void Configure(EntityTypeBuilder<TeamSkillGapDismissal> builder)
    {
        builder.ToTable("TeamSkillGapDismissals");

        builder.HasKey(dismissal => dismissal.Id);

        builder.Property(dismissal => dismissal.OrganizationId).IsRequired();

        builder.Property(dismissal => dismissal.StageKey)
            .IsRequired()
            .HasMaxLength(SkillGapSourceRefs.MaximumStageKeyLength);

        builder.Property(dismissal => dismissal.DismissedBy);
        builder.Property(dismissal => dismissal.DismissedAt).IsRequired();
        builder.Property(dismissal => dismissal.ExpiresAt).IsRequired();
        builder.Property(dismissal => dismissal.AccuracyPercentAtDismissal).IsRequired();
        builder.Property(dismissal => dismissal.AttemptCountAtDismissal).IsRequired();

        builder.Property(dismissal => dismissal.Note)
            .HasMaxLength(500);

        builder.HasIndex(dismissal => new { dismissal.OrganizationId, dismissal.StageKey })
            .IsUnique();
    }
}
