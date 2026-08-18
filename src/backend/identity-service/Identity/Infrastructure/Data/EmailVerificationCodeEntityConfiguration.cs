using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Identity.Features.Auth.Models;

namespace Sellevate.Identity.Infrastructure.Data;

/// <summary>
/// Maps the email verification codes. Indexed by address rather than uniquely keyed on it: the service
/// keeps one live code per address by deleting the previous ones, which is a service invariant and not a
/// database constraint.
/// </summary>
public sealed class EmailVerificationCodeEntityConfiguration : IEntityTypeConfiguration<EmailVerificationCode>
{
    public void Configure(EntityTypeBuilder<EmailVerificationCode> builder)
    {
        builder.ToTable("EmailVerificationCodes");
        builder.HasKey(emailVerificationCode => emailVerificationCode.Id);
        builder.HasIndex(emailVerificationCode => emailVerificationCode.Email);
        builder.Property(emailVerificationCode => emailVerificationCode.Email)
            .IsRequired();
        builder.Property(emailVerificationCode => emailVerificationCode.CodeHash)
            .IsRequired();
    }
}
