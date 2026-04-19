using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.Notifications.Models;

namespace SalesTrainer.Api.Features.Notifications;

public sealed class NotificationEntityConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.HasKey(notification => notification.Id);

        builder.Property(notification => notification.RecipientUserId)
            .IsRequired();

        builder.Property(notification => notification.NotificationType)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(notification => notification.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(notification => notification.Body)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(notification => notification.ActionUrl)
            .HasMaxLength(500);

        builder.Property(notification => notification.RelatedEntityId)
            .HasMaxLength(64);

        builder.Property(notification => notification.IsRead)
            .IsRequired();

        builder.Property(notification => notification.CreatedAt)
            .IsRequired();

        builder.HasIndex(notification => new { notification.RecipientUserId, notification.IsRead });
        builder.HasIndex(notification => new { notification.RecipientUserId, notification.CreatedAt });
    }
}
