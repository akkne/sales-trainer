using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.Features.DialogReviews.Models;

namespace Sellevate.Learning.Infrastructure.Data;

/// <summary>
/// Maps a coaching note or a filed dispute about one graded conversation. The session identifier is the
/// same width as <c>UserDialogScores.SessionId</c>, which is where every value here is copied from.
///
/// <para>
/// The three indexes are the three screens: the manager's inbox — "what has been said to me, and what
/// have I filed", one indexed range per person inside one organization; the РОП's queue, open disputes
/// first and newest first, with kind before status because the queue is always asked for one kind at a
/// time; and everything ever said about one conversation, which is what the transcript screen shows
/// alongside the messages.
/// </para>
/// </summary>
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

        builder.HasIndex(note => new { note.OrganizationId, note.SubjectUserId, note.Status });

        builder.HasIndex(note => new { note.OrganizationId, note.Kind, note.Status, note.CreatedAt });

        builder.HasIndex(note => new { note.OrganizationId, note.SessionId });
    }
}
