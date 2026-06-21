using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.BuildingBlocks.Outbox;

namespace Sellevate.Identity.Infrastructure.Data;

public sealed class OutboxMessageEntityConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");
        builder.HasKey(outboxMessage => outboxMessage.Id);
        builder.Property(outboxMessage => outboxMessage.Topic)
            .IsRequired()
            .HasMaxLength(200);
        builder.Property(outboxMessage => outboxMessage.PartitionKey)
            .IsRequired()
            .HasMaxLength(200);
        builder.Property(outboxMessage => outboxMessage.Payload)
            .IsRequired();
        builder.Property(outboxMessage => outboxMessage.OccurredAt)
            .IsRequired();
        builder.HasIndex(outboxMessage => outboxMessage.DispatchedAt);
    }
}
