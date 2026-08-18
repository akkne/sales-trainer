using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using Sellevate.Analytics;
using Sellevate.Analytics.Common.Constants;
using Sellevate.BuildingBlocks.DependencyInjection;
using Sellevate.BuildingBlocks.HealthChecks;
using Sellevate.BuildingBlocks.Tenancy;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using StackExchange.Redis;

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

builder.Services.AddAnalyticsRedisConnection(builder.Configuration);
builder.Services.AddSellevateEventing(builder.Configuration);
builder.Services.AddAnalyticsServices(builder.Configuration);

builder.Services.AddSellevateHealthChecks()
    .AddRedis()
    .AddKafka();

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

builder.Services.AddAuthorization();

var allowedOrigins = (builder.Configuration[ConfigurationKeys.FrontendUrl] ?? ConfigurationDefaults.FrontendUrl)
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

application.UseHttpMetrics();

application.UseAuthentication();
application.UseAuthorization();

application.UseSellevateTenantContext();

application.MapSellevateHealthChecks();
application.MapMetrics();
application.MapControllers();

application.Run();

public partial class Program { }
