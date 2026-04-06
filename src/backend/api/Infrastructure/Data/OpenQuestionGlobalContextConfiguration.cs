using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SalesTrainer.Api.Infrastructure.Data;

public class OpenQuestionGlobalContext
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ContextText { get; set; } = "";
}

public class OpenQuestionGlobalContextConfiguration : IEntityTypeConfiguration<OpenQuestionGlobalContext>
{
    public void Configure(EntityTypeBuilder<OpenQuestionGlobalContext> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ContextText).HasColumnType("text").IsRequired();
    }
}
