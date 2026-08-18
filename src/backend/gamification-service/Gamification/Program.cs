using System.Text;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Sellevate.BuildingBlocks.DependencyInjection;
using Sellevate.BuildingBlocks.HealthChecks;
using Sellevate.BuildingBlocks.Persistence;
using Sellevate.Gamification.Common.Constants;
using Sellevate.Gamification.DependencyInjection;
using Sellevate.Gamification.Infrastructure.Data;
using Serilog;
using Serilog.Sinks.Grafana.Loki;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
{
    var lokiUrl = context.Configuration[ConfigurationKeys.LokiUrl] ?? ConfigurationKeys.DefaultLokiUrl;

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

builder.Services.AddGamificationDataAccess(builder.Configuration);

builder.Services.AddSellevateEventing(builder.Configuration);
builder.Services.AddGamificationServices(builder.Configuration);

builder.Services.AddSellevateHealthChecks()
    .AddRedis()
    .AddKafka();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<GamificationDbContext>(
        HealthCheckConstants.PostgresCheckName,
        tags: [HealthCheckConstants.ReadinessTag]);

builder.Services.AddHangfire(hangfireConfiguration =>
    hangfireConfiguration.UsePostgreSqlStorage(storageOptions =>
        storageOptions.UseNpgsqlConnection(
            PostgresConnectionStrings.Migrations(builder.Configuration))));
builder.Services.AddHangfireServer();

const int minimumJwtSigningKeyByteCount = 32;
var jwtSigningKey = builder.Configuration[ConfigurationKeys.JwtSigningKey];
if (string.IsNullOrEmpty(jwtSigningKey) || Encoding.UTF8.GetByteCount(jwtSigningKey) < minimumJwtSigningKeyByteCount)
{
    throw new InvalidOperationException(ErrorMessages.JwtSigningKeyTooShort(minimumJwtSigningKeyByteCount));
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

var allowedOrigins = (builder.Configuration[ConfigurationKeys.FrontendUrl] ?? ConfigurationKeys.DefaultFrontendUrl)
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

application.UseGamificationRequestPipeline();

await GamificationStartupInitializer.RunAsync(application);

GamificationRecurringJobRegistrar.Register(application);

application.Run();

public partial class Program { }
