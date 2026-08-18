using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.DialogReviews.Models;

namespace Sellevate.Learning.Infrastructure.Data;

public sealed class DialogReviewNoteEntityConfiguration : IEntityTypeConfiguration<DialogReviewNote>
{
    public void Configure(EntityTypeBuilder<DialogReviewNote> builder)
    {
        builder.ToTable("DialogReviewNotes");

        builder.HasKey(note => note.Id);

        builder.Property(note => note.OrganizationId).IsRequired();

        builder.Property(note => note.Kind)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue(DialogReviewKinds.CoachingNote);

        // Same width as UserDialogScores.SessionId, which is where every value here is copied from.
        builder.Property(note => note.SessionId).IsRequired().HasMaxLength(64);
        builder.Property(note => note.DialogModeKey).IsRequired().HasMaxLength(100);

        builder.Property(note => note.SubjectUserId).IsRequired();
        builder.Property(note => note.AuthorUserId).IsRequired();

        builder.Property(note => note.Comment).IsRequired().HasMaxLength(4000);
        builder.Property(note => note.QuotedText).HasMaxLength(8000);
        builder.Property(note => note.Resolution).HasMaxLength(4000);

        builder.Property(note => note.Status)
            .IsRequired()
            .HasMaxLength(20)
            .HasDefaultValue(DialogReviewStatuses.Open);

        // The manager's inbox: "what has been said to me, and what have I filed" — one indexed range
        // per person inside one organization.
        builder.HasIndex(note => new { note.OrganizationId, note.SubjectUserId, note.Status });

        // The РОП's queue: open disputes first, newest first. Kind before status because the queue is
        // always asked for one kind at a time.
        builder.HasIndex(note => new { note.OrganizationId, note.Kind, note.Status, note.CreatedAt });

        // Everything ever said about one conversation, which is what the transcript screen shows
        // alongside the messages.
        builder.HasIndex(note => new { note.OrganizationId, note.SessionId });
    }
}
