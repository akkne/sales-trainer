using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Sellevate.BuildingBlocks.DependencyInjection;
using Sellevate.BuildingBlocks.HealthChecks;
using Sellevate.BuildingBlocks.Outbox;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Identity.Common.Constants;
using Sellevate.Identity.Common.Security;
using Sellevate.Identity.Eventing;
using Sellevate.Identity.Features.Auth;
using Sellevate.Identity.Features.Avatars;
using Sellevate.Identity.Features.Invites;
using Sellevate.Identity.Features.Onboarding;
using Sellevate.Identity.Features.PlatformAdmin;
using Sellevate.Identity.Features.Profile;
using Sellevate.Identity.Infrastructure;
using Sellevate.Identity.Infrastructure.Data;
using Sellevate.Identity.Infrastructure.Storage.Abstract;
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
                new LokiLabel { Key = "service", Value = "sellevate-identity" },
                new LokiLabel { Key = "env",     Value = context.HostingEnvironment.EnvironmentName }
            ],
            propertiesAsLabels: ["RequestId", "UserId"])
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Sellevate.Identity");
});

// Phase 40.7: identity-db gained its first tenant-scoped table (Invites), so the context now
// carries both tenancy interceptors — the write guard and the one that issues SET LOCAL
// app.organization_id for the row-level-security policy. Never switch this registration to EF
// Core's pooled-context helper (docs/CODESTYLE.md, scripts/tenancy-pool-lint.py).
builder.Services.AddSellevateTenancy();

builder.Services.AddDbContext<IdentityDbContext>((serviceProvider, databaseOptions) =>
    databaseOptions
        .UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
        .AddInterceptors(
            serviceProvider.GetRequiredService<TenantSaveChangesInterceptor>(),
            serviceProvider.GetRequiredService<TenantConnectionInterceptor>()));

// Required by AddSellevateEventing's idempotency store (RedisIdempotencyStore).
// Without this the DI container fails validation at builder.Build() and the
// service crashes on startup.
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

builder.Services.AddSellevateEventing(builder.Configuration);
builder.Services.AddScoped<IUserEventPublisher, KafkaUserEventPublisher>();
builder.Services.AddScoped<IOutboxWriter, IdentityOutboxWriter>();
builder.Services.AddScoped<IOutboxStore, IdentityOutboxStore>();
builder.Services.AddHostedService<OutboxRelayBackgroundService>();

// Phase 40.9: identity-service becomes a consumer for the first time. It needs its own projection
// of the tenant registry because it is the service that mints tokens, and a suspended organization
// has to stop producing them (docs/TENANCY/TENANCY.md §1.1).
builder.Services.AddHostedService<OrganizationReplicaConsumer>();

builder.Services.AddSellevateHealthChecks()
    .AddRedis()
    .AddKafka();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<IdentityDbContext>(
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

builder.Services.AddAvatarStorage(builder.Configuration);
builder.Services.AddSellevateEmail(builder.Configuration);
builder.Services.AddHttpClient();

builder.Services
    .AddAuthenticationFeatureServices(builder.Configuration)
    .AddInviteFeatureServices(builder.Configuration)
    .AddOnboardingFeatureServices()
    .AddPlatformAdminFeatureServices(builder.Configuration)
    .AddProfileFeatureServices();

// Phase 40.23. Guards internal/* — the service-to-service routes that carry no JWT.
builder.Services.AddScoped<InternalServiceAuthFilter>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddControllers();
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
    var startupLogger = serviceScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await DatabaseBootstrapper.EnsureDatabaseExistsAsync(
        builder.Configuration.GetConnectionString("Postgres")!, startupLogger);

    var databaseContext = serviceScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    databaseContext.Database.Migrate();

    var superAdminSeeder = serviceScope.ServiceProvider.GetRequiredService<SuperAdminSeeder>();
    await superAdminSeeder.SeedAsync();

    try
    {
        var objectStorage = serviceScope.ServiceProvider.GetRequiredService<IObjectStorage>();
        await objectStorage.EnsureBucketExistsAsync();

        var defaultAvatarSeeder = serviceScope.ServiceProvider.GetRequiredService<DefaultAvatarSeeder>();
        await defaultAvatarSeeder.SeedAsync();
    }
    catch (Exception exception)
    {
        startupLogger.LogWarning(exception, "Avatar storage init failed at startup; continuing without default avatars");
    }
}

application.Run();

public partial class Program { }
