using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Organization.Features.DemoRequests.Models;

namespace Sellevate.Organization.Features.DemoRequests.Configurations;

/// <summary>
/// The lead table's schema. <c>Id</c> is <c>ValueGeneratedNever</c> because <c>DemoRequestService</c>
/// mints it itself, including for a honeypot submission that is never persisted — the response must
/// still carry a freshly minted identifier either way. <c>WorkEmail</c> carries a non-unique index
/// because the cooldown lookup reads by address, not because the address itself is unique: the same
/// person may legitimately submit again once the cooldown has passed. <c>CreatedAt</c> carries a
/// descending index because the admin list is always read newest-first. Both enums are stored as
/// their name rather than their ordinal, so a value stays readable in the database after either enum
/// is reordered in code.
/// </summary>
public sealed class DemoRequestEntityConfiguration : IEntityTypeConfiguration<DemoRequest>
{
    public void Configure(EntityTypeBuilder<DemoRequest> builder)
    {
        builder.ToTable("DemoRequests");

        builder.HasKey(demoRequest => demoRequest.Id);

        builder.Property(demoRequest => demoRequest.Id)
            .ValueGeneratedNever();

        builder.Property(demoRequest => demoRequest.FullName)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(demoRequest => demoRequest.WorkEmail)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(demoRequest => demoRequest.WorkEmail);

        builder.Property(demoRequest => demoRequest.Phone)
            .HasMaxLength(40);

        builder.Property(demoRequest => demoRequest.CompanyName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(demoRequest => demoRequest.JobTitle)
            .HasMaxLength(120);

        builder.Property(demoRequest => demoRequest.SalesTeamSize)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(demoRequest => demoRequest.Comment)
            .HasMaxLength(2000);

        builder.Property(demoRequest => demoRequest.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(DemoRequestStatus.New);

        builder.Property(demoRequest => demoRequest.ConsentGivenAt)
            .IsRequired();

        builder.Property(demoRequest => demoRequest.MarketingConsentGivenAt);

        builder.Property(demoRequest => demoRequest.CreatedAt)
            .IsRequired();

        builder.HasIndex(demoRequest => demoRequest.CreatedAt)
            .IsDescending();

        builder.Property(demoRequest => demoRequest.UpdatedAt)
            .IsRequired();
    }
}
