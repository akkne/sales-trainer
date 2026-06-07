using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesTrainer.Api.Features.DailyQuotes.Models;

namespace SalesTrainer.Api.Features.DailyQuotes;

public sealed class DailyQuoteEntityConfiguration : IEntityTypeConfiguration<DailyQuote>
{
    public void Configure(EntityTypeBuilder<DailyQuote> builder)
    {
        builder.ToTable("DailyQuotes");

        builder.HasKey(quote => quote.Id);

        builder.HasIndex(quote => quote.Date)
            .IsUnique();

        builder.Property(quote => quote.Date)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(quote => quote.Text)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(quote => quote.Author)
            .IsRequired()
            .HasMaxLength(120);
    }
}
