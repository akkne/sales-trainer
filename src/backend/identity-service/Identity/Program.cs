using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Sellevate.BuildingBlocks.DependencyInjection;
using Sellevate.BuildingBlocks.HealthChecks;
using Sellevate.BuildingBlocks.Persistence;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Identity.Common.Constants;
using Sellevate.Identity.Common.Security;
using Sellevate.Identity.Eventing;
using Sellevate.Identity.Features.Auth;
using Sellevate.Identity.Features.Avatars;
using Sellevate.Identity.Features.Invites;
using Sellevate.Identity.Features.Onboarding;
using Sellevate.Identity.Features.Organizations;
using Sellevate.Identity.Features.PlatformAdmin;
using Sellevate.Identity.Features.Profile;
using Sellevate.Identity.Infrastructure;
using Sellevate.Identity.Infrastructure.Data;
using Sellevate.Identity.Infrastructure.Storage.Abstract;
using Serilog;
using Serilog.Sinks.Grafana.Loki;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
{
    var lokiUrl = context.Configuration[ConfigurationKeys.LokiUrl] ?? ConfigurationDefaults.LokiUrl;

    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console(outputTemplate: LoggingConstants.ConsoleOutputTemplate)
        .WriteTo.GrafanaLoki(
            lokiUrl,
            labels:
            [
                new LokiLabel
                {
                    Key = LoggingConstants.ServiceLabelName,
                    Value = LoggingConstants.ServiceLabelValue
                },
                new LokiLabel
                {
                    Key = LoggingConstants.EnvironmentLabelName,
                    Value = context.HostingEnvironment.EnvironmentName
                }
            ],
            propertiesAsLabels: LoggingConstants.PropertiesPromotedToLabels)
        .Enrich.FromLogContext()
        .Enrich.WithProperty(
            LoggingConstants.ApplicationPropertyName,
            LoggingConstants.ApplicationPropertyValue);
});

builder.Services.AddIdentityDataAccess(builder.Configuration);
builder.Services.AddIdentityEventing(builder.Configuration);

builder.Services.AddSellevateHealthChecks()
    .AddRedis()
    .AddKafka();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<IdentityDbContext>(
        HealthCheckConstants.PostgresCheckName,
        tags: [HealthCheckConstants.ReadinessTag]);

const int minimumJwtSigningKeyByteCount = 32;
var jwtSigningKey = builder.Configuration[ConfigurationKeys.JwtKey];
if (string.IsNullOrEmpty(jwtSigningKey) || Encoding.UTF8.GetByteCount(jwtSigningKey) < minimumJwtSigningKeyByteCount)
{
    throw new InvalidOperationException(ErrorMessages.JwtSigningKeyTooShort);
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

var allowedOrigins = (builder.Configuration[ConfigurationKeys.FrontendUrl] ?? ConfigurationDefaults.FrontendUrl)
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
    .AddOrganizationBootstrapFeatureServices()
    .AddPlatformAdminFeatureServices(builder.Configuration)
    .AddProfileFeatureServices();

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

    await DatabaseMigrator.MigrateAsync<IdentityDbContext>(
        serviceScope.ServiceProvider, builder.Configuration, startupLogger);

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
        startupLogger.LogWarning(exception, ErrorMessages.AvatarStorageInitializationFailed);
    }
}

application.Run();

public partial class Program { }
