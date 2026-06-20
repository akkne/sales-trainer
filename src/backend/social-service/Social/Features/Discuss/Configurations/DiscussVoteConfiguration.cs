using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Social.Features.Discuss.Models;

namespace Sellevate.Social.Features.Discuss.Configurations;

public sealed class DiscussVoteConfiguration : IEntityTypeConfiguration<DiscussVote>
{
    public void Configure(EntityTypeBuilder<DiscussVote> builder)
    {
        builder.ToTable("DiscussVotes");
        builder.HasKey(vote => vote.Id);

        builder.HasIndex(vote => new { vote.UserId, vote.TargetType, vote.TargetId }).IsUnique();
        builder.HasIndex(vote => new { vote.TargetType, vote.TargetId });
    }
}
