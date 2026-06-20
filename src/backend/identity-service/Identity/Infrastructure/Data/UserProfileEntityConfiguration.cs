using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Identity.Features.Onboarding.Models;

namespace Sellevate.Identity.Infrastructure.Data;

public class UserProfileEntityConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("UserProfiles");
        builder.HasKey(profile => profile.Id);
        builder.HasIndex(profile => profile.UserId).IsUnique();
    }
}
