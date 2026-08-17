using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.Programs.Models;

namespace Sellevate.Learning.Infrastructure.Data;

public sealed class ProgramEnrollmentEntityConfiguration : IEntityTypeConfiguration<ProgramEnrollment>
{
    public void Configure(EntityTypeBuilder<ProgramEnrollment> builder)
    {
        builder.ToTable("ProgramEnrollments");

        builder.HasKey(enrollment => enrollment.Id);

        builder.Property(enrollment => enrollment.OrganizationId).IsRequired();
        builder.Property(enrollment => enrollment.UserId).IsRequired();
        builder.Property(enrollment => enrollment.ProgramVersionId).IsRequired();
        builder.Property(enrollment => enrollment.PreviousProgramVersionId);
        builder.Property(enrollment => enrollment.EnrolledAt).IsRequired();
        builder.Property(enrollment => enrollment.SwitchedAt);

        // A real foreign key, unlike the references in ProgramItem, because both sides are strict
        // tenant data under the same plain-equality policy and always in the same organization.
        // Restrict rather than Cascade: a programme version somebody is standing on is not something
        // to delete, and refusing the delete is a far better answer than silently unpinning a
        // learner mid-course.
        builder.HasOne(enrollment => enrollment.ProgramVersion)
            .WithMany()
            .HasForeignKey(enrollment => enrollment.ProgramVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        // One pin per learner per organization. The same person may legitimately belong to two
        // organizations (memberships, 40.6) and then holds one enrollment in each.
        builder.HasIndex(enrollment => new { enrollment.OrganizationId, enrollment.UserId })
            .IsUnique();

        builder.HasIndex(enrollment => enrollment.ProgramVersionId);
    }
}
