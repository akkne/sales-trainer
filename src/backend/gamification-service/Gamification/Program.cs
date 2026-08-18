using System.Text;
using Hangfire;
using Hangfire.PostgreSql;
using Npgsql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Sellevate.BuildingBlocks.DependencyInjection;
using Sellevate.BuildingBlocks.HealthChecks;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Gamification.Common.Constants;
using Sellevate.Gamification.DependencyInjection;
using Sellevate.Gamification.Features.Achievements;
using Sellevate.Gamification.Features.Gamification;
using Sellevate.Gamification.Features.League;
using Sellevate.Gamification.Infrastructure.Data;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
{
    var lokiUrl = context.Configuration["Logging:Loki:Url"] ?? "http://loki:3100";

    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
        .WriteTo.GrafanaLoki(
            lokiUrl,
            labels:
            [
                new LokiLabel { Key = "service", Value = "sellevate-gamification" },
                new LokiLabel { Key = "env",     Value = context.HostingEnvironment.EnvironmentName }
            ],
            propertiesAsLabels: ["RequestId", "UserId"])
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Sellevate.Gamification");
});

// Phase 40.13. Registers the request-scoped ITenantContext plus the two interceptors: the
// cross-tenant write guard and the one that issues SET LOCAL app.organization_id for the
// row-level-security policies. AddSellevateEventing below also calls AddSellevateTenancy, but
// this file registers it explicitly so the DbContext registration below cannot depend on
// the ordering of an unrelated call. Never switch to EF Core's pooled-context helper
// (docs/CODESTYLE.md, scripts/tenancy-pool-lint.py) — a pooled context would cache the first
// tenant's query filter and hand it to every later caller.
builder.Services.AddSellevateTenancy();

builder.Services.AddDbContext<GamificationDbContext>((serviceProvider, databaseOptions) =>
{
    // Pin the Npgsql session timezone to UTC so that DateOnly.FromDateTime(timestamptz)
    // comparisons in XP-sync queries always bucket dates consistently, regardless of
    // the host OS timezone.
    var connectionString = builder.Configuration.GetConnectionString("Postgres");
    var npgsqlBuilder = new NpgsqlConnectionStringBuilder(connectionString)
    {
        Timezone = "UTC",
    };
    databaseOptions
        .UseNpgsql(npgsqlBuilder.ConnectionString)
        .AddInterceptors(
            serviceProvider.GetRequiredService<TenantSaveChangesInterceptor>(),
            serviceProvider.GetRequiredService<TenantConnectionInterceptor>());
});

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

builder.Services.AddSellevateEventing(builder.Configuration);
builder.Services.AddGamificationServices();

builder.Services.AddSellevateHealthChecks()
    .AddRedis()
    .AddKafka();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<GamificationDbContext>(
        HealthCheckConstants.PostgresCheckName,
        tags: [HealthCheckConstants.ReadinessTag]);

builder.Services.AddHangfire(hangfireConfiguration =>
    hangfireConfiguration.UsePostgreSqlStorage(storageOptions =>
        storageOptions.UseNpgsqlConnection(builder.Configuration.GetConnectionString("Postgres"))));
builder.Services.AddHangfireServer();

const int minimumJwtSigningKeyByteCount = 32;
var jwtSigningKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrEmpty(jwtSigningKey) || Encoding.UTF8.GetByteCount(jwtSigningKey) < minimumJwtSigningKeyByteCount)
{
    throw new InvalidOperationException(
        "Jwt:Key must be configured and at least 32 bytes (256 bits) long for HMAC-SHA256.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(jwtOptions =>
    {
        jwtOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey))
        };
    });

builder.Services.AddAuthorization(AuthorizationPolicies.Register);

var allowedOrigins = (builder.Configuration["Frontend:Url"] ?? "http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(corsOptions => corsOptions.AddDefaultPolicy(corsPolicy =>
    corsPolicy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var application = builder.Build();

application.UseExceptionHandler();
application.UseSerilogRequestLogging();
application.UseCors();

if (application.Environment.IsDevelopment())
{
    application.UseSwagger();
    application.UseSwaggerUI();
}

application.UseAuthentication();
application.UseAuthorization();

// Phase 40.13. Populates the scoped ITenantContext from the gateway-validated X-Organization-Id
// header. After UseAuthorization so the endpoint — and therefore its [TenantScoped] metadata —
// is already resolved.
application.UseSellevateTenantContext();

application.MapSellevateHealthChecks();

application.MapControllers();

using (var serviceScope = application.Services.CreateScope())
{
    // Phase 40.13. Startup has no request and therefore no organization. Everything seeded below is
    // platform-global by design — the achievement catalogue, the league tiers and the installation
    // -wide GamificationSettings row — so this scope declares system mode explicitly rather than
    // running on a blank context that would be indistinguishable from a forgotten one
    // (docs/TENANCY/TENANCY.md §1.6). LeagueSettings is deliberately no longer seeded here: it
    // became tenant-scoped, and a row seeded with no organization is hidden from everybody by the
    // RLS policy.
    serviceScope.ServiceProvider.GetRequiredService<TenantContext>().EnterSystemMode();

    var startupLogger = serviceScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await DatabaseBootstrapper.EnsureDatabaseExistsAsync(
        builder.Configuration.GetConnectionString("Postgres")!, startupLogger);

    var databaseContext = serviceScope.ServiceProvider.GetRequiredService<GamificationDbContext>();
    databaseContext.Database.Migrate();

    var achievementSeeder = serviceScope.ServiceProvider.GetRequiredService<AchievementSeeder>();
    await achievementSeeder.SeedAsync();

    // GA6(a): seed singleton settings rows so read-path getters never write.
    var leagueSettingsSeeder = serviceScope.ServiceProvider.GetRequiredService<LeagueSettingsSeeder>();
    await leagueSettingsSeeder.SeedAsync();
}

// Use the DI-resolved IRecurringJobManager, not the static RecurringJob facade:
// the static API reads JobStorage.Current, which is only set once the Hangfire
// server has started — calling it here (before application.Run()) throws
// "Current JobStorage instance has not been initialized yet".
using (var recurringJobScope = application.Services.CreateScope())
{
    var recurringJobManager = recurringJobScope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

    recurringJobManager.AddOrUpdate<WeeklyLeagueClosureJob>(
        HangfireJobIdentifiers.WeeklyLeagueClosure,
        weeklyLeagueClosureJob => weeklyLeagueClosureJob.ExecuteAsync(),
        HangfireJobIdentifiers.WeeklyLeagueClosureCron);

    recurringJobManager.AddOrUpdate<StreakResetJob>(
        HangfireJobIdentifiers.DailyStreakReset,
        streakResetJob => streakResetJob.ExecuteAsync(),
        HangfireJobIdentifiers.DailyStreakResetCron);
}

application.Run();

public partial class Program { }
