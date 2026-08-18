using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sellevate.Learning.Features.DailyQuotes.Models;

namespace Sellevate.Learning.Infrastructure.Data;

/// <summary>
/// Maps the daily quote. Global content with no organization column, and unique on the date: exactly one
/// quote is shown per day to everybody, so a second row for the same date would make the choice
/// nondeterministic.
/// </summary>
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
