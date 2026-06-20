using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Identity.Features.Auth.Models;

namespace Sellevate.Identity.Infrastructure.Data;

public class EmailVerificationCodeEntityConfiguration : IEntityTypeConfiguration<EmailVerificationCode>
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
