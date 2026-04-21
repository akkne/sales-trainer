using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.Techniques.Models;

namespace SalesTrainer.Api.Features.Techniques;

public sealed class UserTechniqueProgressEntityConfiguration : IEntityTypeConfiguration<UserTechniqueProgress>
{
    public void Configure(EntityTypeBuilder<UserTechniqueProgress> builder)
    {
        builder.ToTable("UserTechniqueProgress");

        builder.HasKey(progress => progress.Id);

        builder.HasIndex(progress => new { progress.UserId, progress.TechniqueId })
            .IsUnique();

        builder.HasIndex(progress => progress.UserId);
    }
}
