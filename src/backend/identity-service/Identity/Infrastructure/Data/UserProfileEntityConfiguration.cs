using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Identity.Features.Onboarding.Models;

namespace Sellevate.Identity.Infrastructure.Data;

/// <summary>
/// Maps the profile table. The unique index on <c>UserId</c> is what makes the relationship one-to-one,
/// which every upsert in the profile and onboarding services relies on.
/// </summary>
public sealed class UserProfileEntityConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("UserProfiles");
        builder.HasKey(profile => profile.Id);
        builder.HasIndex(profile => profile.UserId).IsUnique();
    }
}
