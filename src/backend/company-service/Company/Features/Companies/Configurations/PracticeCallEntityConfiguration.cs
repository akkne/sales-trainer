using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Company.Common.Constants;
using Sellevate.Company.Features.Companies.Models;

namespace Sellevate.Company.Features.Companies.Configurations;

/// <summary>
/// Maps the <c>PracticeCalls</c> table — the link between a company and a practice conversation
/// held in ai-service.
///
/// <para>
/// <c>DialogSessionId</c> is a foreign identifier owned by ai-service and is stored as an opaque
/// string with no constraint against it: company-db cannot reference another service's store, so a
/// session that no longer exists there leaves a row here that resolves to nothing rather than a
/// broken join.
/// </para>
/// </summary>
internal sealed class PracticeCallEntityConfiguration : IEntityTypeConfiguration<PracticeCall>
{
    public void Configure(EntityTypeBuilder<PracticeCall> builder)
    {
        builder.ToTable("PracticeCalls");

        builder.HasKey(practiceCall => practiceCall.Id);

        builder.Property(practiceCall => practiceCall.OrganizationId)
            .IsRequired();

        builder.Property(practiceCall => practiceCall.DialogSessionId)
            .IsRequired()
            .HasMaxLength(CompanyFieldLengths.DialogSessionId);

        builder.Property(practiceCall => practiceCall.Goal)
            .IsRequired()
            .HasMaxLength(CompanyFieldLengths.PracticeCallGoal);

        builder.Property(practiceCall => practiceCall.CreatedAt)
            .IsRequired();

        builder.HasIndex(practiceCall => new { practiceCall.OrganizationId, practiceCall.CompanyId, practiceCall.CreatedAt })
            .HasDatabaseName("IX_PracticeCalls_OrganizationId_CompanyId_CreatedAt")
            .IsDescending(false, false, true);

        builder.HasIndex(practiceCall => new { practiceCall.OrganizationId, practiceCall.UserId })
            .HasDatabaseName("IX_PracticeCalls_OrganizationId_UserId");
    }
}
