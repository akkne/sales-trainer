using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Sellevate.BuildingBlocks.HealthChecks;
using Sellevate.Company.Common.Constants;
using Sellevate.Company.Features.Companies;
using Sellevate.Company.Infrastructure.Data;
using Serilog;
using Serilog.Sinks.Grafana.Loki;

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
                new LokiLabel { Key = "service", Value = "sellevate-company" },
                new LokiLabel { Key = "env",     Value = context.HostingEnvironment.EnvironmentName }
            ],
            propertiesAsLabels: ["RequestId", "UserId"])
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Sellevate.Company");
});

builder.Services.AddDbContext<CompanyDbContext>(databaseOptions =>
    databaseOptions.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddCompanyFeatureServices();

builder.Services.AddSellevateHealthChecks();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<CompanyDbContext>(
        CompanyHealthCheckConstants.PostgresCheckName,
        tags: [CompanyHealthCheckConstants.ReadinessTag]);

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

builder.Services.AddAuthorization();

var allowedOrigins = (builder.Configuration["Frontend:Url"] ?? "http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(corsOptions => corsOptions.AddDefaultPolicy(corsPolicy =>
    corsPolicy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

builder.Services.AddControllers();
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

application.MapSellevateHealthChecks();

application.MapControllers();

using (var serviceScope = application.Services.CreateScope())
{
    var startupLogger = serviceScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await DatabaseBootstrapper.EnsureDatabaseExistsAsync(
        builder.Configuration.GetConnectionString("Postgres")!, startupLogger);

    var databaseContext = serviceScope.ServiceProvider.GetRequiredService<CompanyDbContext>();
    await databaseContext.Database.MigrateAsync();
}

application.Run();

public partial class Program { }
