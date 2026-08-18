using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Sellevate.BuildingBlocks.DependencyInjection;
using Sellevate.BuildingBlocks.HealthChecks;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.BuildingBlocks.Persistence;
using Sellevate.BuildingBlocks.Tenancy;
using Sellevate.Company.Common.Constants;
using Sellevate.Company.Features.Companies;
using Sellevate.Company.Infrastructure.Data;
using Serilog;
using Serilog.Sinks.Grafana.Loki;

const string defaultLokiUrl = "http://loki:3100";
const string defaultFrontendOrigin = "http://localhost:3000";
const int minimumJwtSigningKeyByteCount = 32;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
{
    var lokiUrl = context.Configuration[CompanyConfigurationKeys.LokiUrl] ?? defaultLokiUrl;

    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
        .WriteTo.GrafanaLoki(
            lokiUrl,
            labels:
            [
                new LokiLabel { Key = "service", Value = "sellevate-company" },
                new LokiLabel { Key = "env",     Value = context.HostingEnvironment.EnvironmentName }
            ],
            propertiesAsLabels: ["RequestId", "UserId"])
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Sellevate.Company");
});

builder.Services.AddSellevateTenancy();

builder.Services.AddDbContext<CompanyDbContext>((serviceProvider, databaseOptions) =>
    databaseOptions
        .UseNpgsql(builder.Configuration.GetConnectionString(CompanyConfigurationKeys.PostgresConnectionName))
        .AddInterceptors(
            serviceProvider.GetRequiredService<TenantSaveChangesInterceptor>(),
            serviceProvider.GetRequiredService<TenantConnectionInterceptor>()));

builder.Services.AddCompanyFeatureServices(builder.Configuration);

builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection(KafkaSettings.SectionName));
builder.Services.AddHostedService<KafkaTopicProvisioner>();
builder.Services.AddSingleton<KafkaEventPublisher>();
builder.Services.AddSingleton<IEventPublisher>(serviceProvider => serviceProvider.GetRequiredService<KafkaEventPublisher>());
builder.Services.AddCompanyFollowUpReminders(builder.Configuration);

builder.Services.AddSellevateHealthChecks()
    .AddKafka();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<CompanyDbContext>(
        CompanyHealthCheckConstants.PostgresCheckName,
        tags: [CompanyHealthCheckConstants.ReadinessTag]);

var jwtSigningKey = builder.Configuration[CompanyConfigurationKeys.JwtKey];
if (string.IsNullOrEmpty(jwtSigningKey) || Encoding.UTF8.GetByteCount(jwtSigningKey) < minimumJwtSigningKeyByteCount)
{
    throw new InvalidOperationException(CompanyErrorMessages.JwtSigningKeyMisconfigured);
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(jwtOptions =>
    {
        jwtOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration[CompanyConfigurationKeys.JwtIssuer],
            ValidateAudience = true,
            ValidAudience = builder.Configuration[CompanyConfigurationKeys.JwtAudience],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey))
        };
    });

builder.Services.AddAuthorization();

var allowedOrigins = (builder.Configuration[CompanyConfigurationKeys.FrontendUrl] ?? defaultFrontendOrigin)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(corsOptions => corsOptions.AddDefaultPolicy(corsPolicy =>
    corsPolicy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

builder.Services.AddControllers()
    .AddJsonOptions(jsonOptions =>
        jsonOptions.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddProblemDetails();

var application = builder.Build();

application.UseSerilogRequestLogging();
application.UseExceptionHandler();
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

    await DatabaseMigrator.MigrateAsync<CompanyDbContext>(
        serviceScope.ServiceProvider, builder.Configuration, startupLogger);
}

application.Run();

public partial class Program { }
