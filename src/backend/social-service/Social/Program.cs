using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Sellevate.BuildingBlocks.DependencyInjection;
using Sellevate.BuildingBlocks.HealthChecks;
using Sellevate.Social.Eventing;
using Sellevate.Social.Infrastructure.HealthChecks;
using Sellevate.Social.Features.Discuss;
using Sellevate.Social.Features.Friends;
using Sellevate.Social.Infrastructure.Configuration;
using Sellevate.Social.Infrastructure.Data;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Social.Infrastructure.Mongo;
using Sellevate.Social.Infrastructure.Storage;
using Sellevate.Social.Infrastructure.Storage.Abstract;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using StackExchange.Redis;
using Sellevate.Social.Common.Constants;

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
                new LokiLabel { Key = "service", Value = "sellevate-social" },
                new LokiLabel { Key = "env",     Value = context.HostingEnvironment.EnvironmentName }
            ],
            propertiesAsLabels: ["RequestId", "UserId"])
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Sellevate.Social");
});

BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

// Phase 40.13. Registers the request-scoped ITenantContext plus the two interceptors: the
// cross-tenant write guard and the one that issues SET LOCAL app.organization_id for the
// row-level-security policies. Never switch this to EF Core's pooled-context helper
// (docs/CODESTYLE.md, scripts/tenancy-pool-lint.py) — a pooled context would cache the first
// tenant's query filter and hand it to every later caller.
builder.Services.AddSellevateTenancy();

builder.Services.AddDbContext<SocialDbContext>((serviceProvider, databaseOptions) =>
    databaseOptions
        .UseNpgsql(builder.Configuration.GetConnectionString("Postgres"))
        .AddInterceptors(
            serviceProvider.GetRequiredService<TenantSaveChangesInterceptor>(),
            serviceProvider.GetRequiredService<TenantConnectionInterceptor>()));

builder.Services.Configure<MongoConfiguration>(builder.Configuration.GetSection(MongoConfiguration.SectionName));
builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(builder.Configuration.GetConnectionString("Mongo")));
builder.Services.AddSingleton<MongoDbContext>();

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

builder.Services.AddSellevateEventing(builder.Configuration);
builder.Services.AddScoped<ISocialEventPublisher, KafkaSocialEventPublisher>();
builder.Services.AddHostedService<UserReplicaConsumer>();

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

// Phase 40.13. Populates the request-scoped ITenantContext from the X-Organization-Id header the
// gateway sets from the validated token (and strips from inbound requests). After UseAuthorization
// so the endpoint — and therefore its [TenantScoped] metadata — is already resolved: a controller
// marked [TenantScoped] gets 403 rather than running with no tenant.
application.UseSellevateTenantContext();

application.MapSellevateHealthChecks();

application.MapControllers();

using (var serviceScope = application.Services.CreateScope())
{
    var startupLogger = serviceScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await DatabaseBootstrapper.EnsureDatabaseExistsAsync(
        builder.Configuration.GetConnectionString("Postgres")!, startupLogger);

    var databaseContext = serviceScope.ServiceProvider.GetRequiredService<SocialDbContext>();
    databaseContext.Database.Migrate();

    var objectStorage = serviceScope.ServiceProvider.GetRequiredService<IObjectStorage>();
    await objectStorage.EnsureBucketExistsAsync();
}

application.Run();

public partial class Program { }
