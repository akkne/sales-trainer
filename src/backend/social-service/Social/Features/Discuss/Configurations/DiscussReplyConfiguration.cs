using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Social.Features.Discuss.Models;

namespace Sellevate.Social.Features.Discuss.Configurations;

public sealed class DiscussReplyConfiguration : IEntityTypeConfiguration<DiscussReply>
{
    public void Configure(EntityTypeBuilder<DiscussReply> builder)
    {
        builder.ToTable("DiscussReplies");
        builder.HasKey(reply => reply.Id);

        builder.Property(reply => reply.Body).IsRequired().HasMaxLength(20000);

        builder.HasIndex(reply => reply.AuthorId);
        builder.HasIndex(reply => new { reply.ThreadId, reply.CreatedAt });
    }
}
