using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.Auth.Models;
using SalesTrainer.Api.Features.Discuss.Models;

namespace SalesTrainer.Api.Features.Discuss.Configurations;

public class DiscussReplyConfiguration : IEntityTypeConfiguration<DiscussReply>
{
    public void Configure(EntityTypeBuilder<DiscussReply> builder)
    {
        builder.ToTable("DiscussReplies");
        builder.HasKey(reply => reply.Id);

        builder.Property(reply => reply.Body).IsRequired().HasMaxLength(20000);

        builder.HasOne(reply => reply.Thread)
            .WithMany(thread => thread.Replies)
            .HasForeignKey(reply => reply.ThreadId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(reply => reply.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(reply => reply.AuthorId);
        builder.HasIndex(reply => new { reply.ThreadId, reply.CreatedAt });
    }
}
