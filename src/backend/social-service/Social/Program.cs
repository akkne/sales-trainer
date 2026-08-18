using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Sellevate.BuildingBlocks.DependencyInjection;
using Sellevate.BuildingBlocks.HealthChecks;
using Sellevate.BuildingBlocks.Persistence;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Social.Common.Constants;
using Sellevate.Social.Eventing;
using Sellevate.Social.Features.Discuss;
using Sellevate.Social.Features.Friends;
using Sellevate.Social.Infrastructure.Data;
using Sellevate.Social.Infrastructure.HealthChecks;
using Sellevate.Social.Infrastructure.Mongo;
using Sellevate.Social.Infrastructure.Storage;
using Sellevate.Social.Infrastructure.Storage.Abstract;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using StackExchange.Redis;

const string DefaultLokiUrl = "http://loki:3100";
const string DefaultFrontendUrl = "http://localhost:3000";

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
{
    var lokiUrl = context.Configuration[ConfigurationKeys.LokiUrl] ?? DefaultLokiUrl;

    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
        .WriteTo.GrafanaLoki(
            lokiUrl,
            labels:
            [
                new LokiLabel { Key = "service", Value = "sellevate-social" },
                new LokiLabel { Key = "env",     Value = context.HostingEnvironment.EnvironmentName }
            ],
            propertiesAsLabels: ["RequestId", "UserId"])
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Sellevate.Social");
});

BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

builder.Services.AddSellevateTenancy();

builder.Services.AddDbContext<SocialDbContext>((serviceProvider, databaseOptions) =>
    databaseOptions
        .UseNpgsql(builder.Configuration.GetConnectionString(
            ConfigurationKeys.ConnectionStringNames.Postgres))
        .AddInterceptors(
            serviceProvider.GetRequiredService<TenantSaveChangesInterceptor>(),
            serviceProvider.GetRequiredService<TenantConnectionInterceptor>()));

builder.Services.AddSocialMongo(builder.Configuration);

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString(
        ConfigurationKeys.ConnectionStringNames.Redis)!));

builder.Services.AddSocialEventing(builder.Configuration);

builder.Services.AddSellevateHealthChecks()
    .AddRedis()
    .AddKafka();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SocialDbContext>(
        HealthCheckConstants.PostgresCheckName,
        tags: [HealthCheckConstants.ReadinessTag])
    .AddCheck<MongoHealthCheck>(
        HealthCheckConstants.MongoCheckName,
        tags: [HealthCheckConstants.ReadinessTag]);

builder.Services.AddSocialObjectStorage(builder.Configuration);

builder.Services
    .AddFriendFeatureServices()
    .AddDiscussFeatureServices();

const int minimumJwtSigningKeyByteCount = 32;
var jwtSigningKey = builder.Configuration[ConfigurationKeys.JwtKey];
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
            ValidIssuer = builder.Configuration[ConfigurationKeys.JwtIssuer],
            ValidateAudience = true,
            ValidAudience = builder.Configuration[ConfigurationKeys.JwtAudience],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey))
        };
    });

builder.Services.AddAuthorization(AuthorizationPolicies.Register);

var allowedOrigins = (builder.Configuration[ConfigurationKeys.FrontendUrl] ?? DefaultFrontendUrl)
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
    var startupLogger = serviceScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await DatabaseMigrator.MigrateAsync<SocialDbContext>(
        serviceScope.ServiceProvider, builder.Configuration, startupLogger);

    var objectStorage = serviceScope.ServiceProvider.GetRequiredService<IObjectStorage>();
    await objectStorage.EnsureBucketExistsAsync();
}

application.Run();

/// <summary>
/// Startup for social-service. Two orderings in this file are load-bearing and neither is obvious
/// from reading it top to bottom.
///
/// <para>
/// Phase 40.13. <c>AddSellevateTenancy</c> registers the request-scoped <c>ITenantContext</c> plus two
/// interceptors — the cross-tenant write guard, and the one that issues
/// <c>SET LOCAL app.organization_id</c> for the row-level-security policies. The context is registered
/// with plain <c>AddDbContext</c> and must never move to EF Core's pooled-context helper
/// (docs/CODESTYLE.md, <c>scripts/tenancy-pool-lint.py</c>): a pooled context caches the first tenant's
/// query filter and hands it to every later caller.
/// </para>
///
/// <para>
/// <c>UseSellevateTenantContext</c> populates that context from the <c>X-Organization-Id</c> header the
/// gateway sets from the validated token (and strips from inbound requests). It runs after
/// <c>UseAuthorization</c> so the endpoint — and therefore its <c>[TenantScoped]</c> metadata — is
/// already resolved: a controller marked <c>[TenantScoped]</c> then answers 403 instead of running with
/// no tenant.
/// </para>
/// </summary>
public partial class Program { }
