using Microsoft.EntityFrameworkCore;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Gamification.Common.Constants;
using Sellevate.Gamification.Features.Achievements;
using Sellevate.Gamification.Features.League;

namespace Sellevate.Gamification.Infrastructure.Data;

/// <summary>
/// Brings gamification-db up to date before the first request: creates the database if it is
/// missing, applies migrations, then seeds the platform-global catalogues.
///
/// <para>
/// Phase 40.13. Startup has no request and therefore no organization. Everything seeded here is
/// platform-global by design — the achievement catalogue, the league tiers and the installation-wide
/// <c>GamificationSettings</c> row — so the scope declares system mode <b>explicitly</b> rather than
/// running on a blank context that would be indistinguishable from a forgotten one
/// (docs/TENANCY/TENANCY.md §1.6). <c>LeagueSettings</c> is deliberately not seeded: it became
/// tenant-scoped, and a row seeded with no organization is hidden from everybody by the RLS policy.
/// </para>
///
/// <para>
/// Ordering is load-bearing throughout: the database must exist before migrations can run, and the
/// schema must exist before a seeder can query it.
/// </para>
/// </summary>
internal static class GamificationStartupInitializer
{
    public static async Task RunAsync(WebApplication application, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(application);

        using var serviceScope = application.Services.CreateScope();

        serviceScope.ServiceProvider.GetRequiredService<TenantContext>().EnterSystemMode();

        var startupLogger = serviceScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        await DatabaseBootstrapper.EnsureDatabaseExistsAsync(
            application.Configuration.GetConnectionString(ConfigurationKeys.PostgresConnectionName)!,
            startupLogger,
            cancellationToken);

        var databaseContext = serviceScope.ServiceProvider.GetRequiredService<GamificationDbContext>();
        databaseContext.Database.Migrate();

        var achievementSeeder = serviceScope.ServiceProvider.GetRequiredService<AchievementSeeder>();
        await achievementSeeder.SeedAsync(cancellationToken);

        var gamificationSettingsSeeder = serviceScope.ServiceProvider.GetRequiredService<LeagueSettingsSeeder>();
        await gamificationSettingsSeeder.SeedAsync(cancellationToken);
    }
}
