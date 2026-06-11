using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Features.Discuss.Models;

namespace SalesTrainer.Api.Features.Discuss.Configurations;

public class DiscussVoteConfiguration : IEntityTypeConfiguration<DiscussVote>
{
    public void Configure(EntityTypeBuilder<DiscussVote> builder)
    {
        builder.ToTable("DiscussVotes");
        builder.HasKey(vote => vote.Id);

        builder.Property(vote => vote.TargetType).IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(vote => vote.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Prevents double-voting; existence of a row means "upvoted".
        builder.HasIndex(vote => new { vote.UserId, vote.TargetType, vote.TargetId }).IsUnique();
        builder.HasIndex(vote => new { vote.TargetType, vote.TargetId });
    }
}
