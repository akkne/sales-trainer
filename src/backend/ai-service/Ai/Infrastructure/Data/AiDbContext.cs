using Microsoft.EntityFrameworkCore;
using Sellevate.Ai.Features.Dialog;
using Sellevate.Ai.Features.Dialog.Models;
using Sellevate.Ai.Identity;

namespace Sellevate.Ai.Infrastructure.Data;

public sealed class AiDbContext : DbContext
{
    public AiDbContext(DbContextOptions<AiDbContext> options) : base(options)
    {
    }

    public DbSet<DialogBundle> DialogBundles => Set<DialogBundle>();
    public DbSet<DialogMode> DialogModes => Set<DialogMode>();
    public DbSet<UserReplica> UserReplicas => Set<UserReplica>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new DialogBundleConfiguration());
        modelBuilder.ApplyConfiguration(new DialogModeConfiguration());
        modelBuilder.ApplyConfiguration(new UserReplicaEntityConfiguration());
    }
}
