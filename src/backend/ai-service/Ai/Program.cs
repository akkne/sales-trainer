using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Sellevate.Ai.Eventing;
using Sellevate.Ai.Features.Dialog;
using Sellevate.Ai.Features.Dialog.Seeders;
using Sellevate.Ai.Features.Evaluation;
using Sellevate.Ai.Features.Transcription;
using Sellevate.Ai.Features.Voice;
using Sellevate.Ai.Infrastructure.Data;
using Sellevate.Ai.Infrastructure.HealthChecks;
using Sellevate.Ai.Infrastructure.Http;
using Sellevate.Ai.Infrastructure.Mongo;
using Sellevate.BuildingBlocks.DependencyInjection;
using Sellevate.BuildingBlocks.HealthChecks;
using Sellevate.BuildingBlocks.Messaging;
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
                new LokiLabel { Key = "service", Value = "sellevate-ai" },
                new LokiLabel { Key = "env",     Value = context.HostingEnvironment.EnvironmentName }
            ],
            propertiesAsLabels: ["RequestId", "UserId"])
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Sellevate.Ai");
});

BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

builder.Services.AddDbContext<AiDbContext>(databaseOptions =>
    databaseOptions.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(builder.Configuration.GetConnectionString("Mongo")));
builder.Services.AddSingleton<MongoDbContext>();

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

builder.Services.AddSellevateEventing(builder.Configuration);
builder.Services.AddScoped<IDialogEventPublisher, KafkaDialogEventPublisher>();
builder.Services.AddSingleton<IDialogScoringWeightsProvider, DialogScoringWeightsProvider>();
builder.Services.AddHostedService<GamificationDialogWeightsConsumer>();
builder.Services.AddHostedService<UserReplicaConsumer>();

builder.Services.AddSellevateHealthChecks()
    .AddRedis()
    .AddKafka();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AiDbContext>(
        HealthCheckConstants.PostgresCheckName,
        tags: [HealthCheckConstants.ReadinessTag])
    .AddCheck<MongoHealthCheck>(
        HealthCheckConstants.MongoCheckName,
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

builder.Services.AddAuthorization(authorizationOptions =>
{
    authorizationOptions.AddPolicy("RequireAdmin", policy =>
        policy.RequireAssertion(authorizationContext =>
            authorizationContext.User.IsInRole("Admin") || authorizationContext.User.IsInRole("SuperAdmin")));
    authorizationOptions.AddPolicy("RequireSuperAdmin", policy =>
        policy.RequireRole("SuperAdmin"));
});

var allowedOrigins = (builder.Configuration["Frontend:Url"] ?? "http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(corsOptions => corsOptions.AddDefaultPolicy(corsPolicy =>
    corsPolicy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

builder.Services
    .AddDialogFeatureServices(builder.Configuration)
    .AddTranscriptionFeatureServices(builder.Configuration)
    .AddVoiceFeatureServices(builder.Configuration)
    .AddEvaluationFeatureServices();

// AI6: add Polly resilience (retry on 5xx/429/timeout + circuit-breaker) to all upstream HTTP clients.
// HttpClient.Timeout is set to 90s so Polly's own timeout (30s per attempt × 3) controls individual calls.
foreach (var upstreamClientName in new[] { "OpenAI", "YandexTts" })
{
    builder.Services.AddHttpClient(upstreamClientName)
        .ConfigureHttpClient(client =>
            client.Timeout = TimeSpan.FromSeconds(90)) // outer timeout > Polly total; Polly controls per-attempt
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(10),
            PooledConnectionLifetime = TimeSpan.FromMinutes(30),
        })
        .SetHandlerLifetime(TimeSpan.FromMinutes(30))
        .AddStandardResilienceHandler(options =>
        {
            // Per-attempt timeout (replaces flat 30s).
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
            // Retry: up to 2 retries on 5xx / 429 / timeout (total ≤ 3 attempts).
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.FromSeconds(1);
            // Circuit breaker: open after 5 failures in a 60s window. Polly requires
            // SamplingDuration >= 2 x AttemptTimeout (2 x 30s), else it fails validation
            // at host startup.
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
            options.CircuitBreaker.MinimumThroughput = 5;
            // Total timeout across all retries.
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(90);
        });
}
builder.Services.AddHttpClient();
builder.Services.AddSingleton<UpstreamConnectionWarmup>();
builder.Services.AddHostedService<UpstreamConnectionWarmupService>();

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

application.MapSellevateHealthChecks();

application.MapControllers();

using (var serviceScope = application.Services.CreateScope())
{
    var startupLogger = serviceScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await DatabaseBootstrapper.EnsureDatabaseExistsAsync(
        builder.Configuration.GetConnectionString("Postgres")!, startupLogger);

    var databaseContext = serviceScope.ServiceProvider.GetRequiredService<AiDbContext>();
    databaseContext.Database.Migrate();

    await CompanyCallModeSeeder.SeedAsync(databaseContext);
}

application.Run();

public partial class Program { }
