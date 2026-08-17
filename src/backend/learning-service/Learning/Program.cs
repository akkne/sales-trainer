using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Sellevate.BuildingBlocks.DependencyInjection;
using Sellevate.BuildingBlocks.HealthChecks;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Learning.Common.Constants;
using Sellevate.Learning.DependencyInjection;
using Sellevate.Learning.Features.Lessons.Services.Abstract;
using Sellevate.Learning.Infrastructure.Data;
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
                new LokiLabel { Key = "service", Value = "sellevate-learning" },
                new LokiLabel { Key = "env",     Value = context.HostingEnvironment.EnvironmentName }
            ],
            propertiesAsLabels: ["RequestId", "UserId"])
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Sellevate.Learning");
});

// Phase 40.10: learning-db is the first database where tenant data (progress) and the global
// content library live side by side, so the context carries both tenancy interceptors — the write
// guard and the one that issues SET LOCAL app.organization_id for the row-level-security policies.
// Never switch this to EF Core's pooled-context helper (docs/CODESTYLE.md,
// scripts/tenancy-pool-lint.py).
builder.Services.AddSellevateTenancy();

builder.Services.AddDbContext<LearningDbContext>((serviceProvider, databaseOptions) =>
    databaseOptions
        .UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
        .AddInterceptors(
            serviceProvider.GetRequiredService<TenantSaveChangesInterceptor>(),
            serviceProvider.GetRequiredService<TenantConnectionInterceptor>()));

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

builder.Services.AddSellevateEventing(builder.Configuration);
builder.Services.AddLearningServices(builder.Configuration);

builder.Services.AddSellevateHealthChecks()
    .AddRedis()
    .AddKafka();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<LearningDbContext>(
        HealthCheckConstants.PostgresCheckName,
        tags: [HealthCheckConstants.ReadinessTag]);

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
application.UseSellevateTenantContext();

application.MapSellevateHealthChecks();

application.MapControllers();

using (var serviceScope = application.Services.CreateScope())
{
    // Phase 40.16. Startup has no request and therefore no organization, so the scope declares
    // system mode explicitly rather than running on a blank context indistinguishable from a
    // forgotten one (docs/TENANCY/TENANCY.md §1.6) — the same shape gamification-service uses for
    // its seeders.
    serviceScope.ServiceProvider.GetRequiredService<TenantContext>().EnterSystemMode();

    var startupLogger = serviceScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await DatabaseBootstrapper.EnsureDatabaseExistsAsync(
        builder.Configuration.GetConnectionString("Postgres")!, startupLogger);

    var databaseContext = serviceScope.ServiceProvider.GetRequiredService<LearningDbContext>();
    databaseContext.Database.Migrate();

    // Phase 40.16. Gives lessons that have never been published a version 1, so the historical
    // progress backfill has something to bind existing attempts to. Idempotent and a no-op on every
    // start after the first; it cannot be SQL inside the migration, because the snapshot's hash is
    // defined over bytes only LessonSnapshotSerializer produces (see ILessonVersionBackfill).
    var lessonVersionBackfill = serviceScope.ServiceProvider.GetRequiredService<ILessonVersionBackfill>();
    await lessonVersionBackfill.BackfillMissingInitialVersionsAsync();
}

application.Run();

public partial class Program { }
