using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.Identity.Eventing;
using Sellevate.Identity.Features.Auth;
using Sellevate.Identity.Features.Avatars;
using Sellevate.Identity.Features.Onboarding;
using Sellevate.Identity.Features.Profile;
using Sellevate.Identity.Infrastructure.Data;
using Sellevate.Identity.Infrastructure.Email;
using Sellevate.Identity.Infrastructure.Storage.Abstract;

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

// ── Persistence: the service's own Postgres database (identity-db) ─────────────
builder.Services.AddDbContext<IdentityDbContext>(databaseOptions =>
    databaseOptions.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

// ── Kafka producer (user.* events) ────────────────────────────────────────────
// Identity only PRODUCES events, so it needs the publisher but no idempotency store /
// Redis (those are for consumers). The domain-facing wrapper keeps topic names out of
// the feature code.
builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection(KafkaSettings.SectionName));
builder.Services.AddSingleton<IEventPublisher, KafkaEventPublisher>();
builder.Services.AddScoped<IUserEventPublisher, KafkaUserEventPublisher>();

// ── AuthN/AuthZ: Identity is the sole JWT issuer and validates its own tokens ───
var jwtSigningKey = builder.Configuration["Jwt:Key"]!;
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
        policy.RequireAssertion(context =>
            context.User.IsInRole("Admin") || context.User.IsInRole("SuperAdmin")));
    authorizationOptions.AddPolicy("RequireSuperAdmin", policy =>
        policy.RequireRole("SuperAdmin"));
});

// CORS for standalone local dev (when the frontend hits the service directly rather
// than through the gateway). Behind the gateway requests are same-origin.
var allowedOrigins = (builder.Configuration["Frontend:Url"] ?? "http://localhost:3000")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(corsOptions => corsOptions.AddDefaultPolicy(corsPolicy =>
    corsPolicy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

builder.Services.AddAvatarStorage(builder.Configuration);
builder.Services.AddEmailServices(builder.Configuration);
builder.Services.AddHttpClient();

builder.Services
    .AddAuthenticationFeatureServices(builder.Configuration)
    .AddOnboardingFeatureServices()
    .AddProfileFeatureServices();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var application = builder.Build();

application.UseSerilogRequestLogging();
application.UseCors();

if (application.Environment.IsDevelopment())
{
    application.UseSwagger();
    application.UseSwaggerUI();
}

application.UseAuthentication();
application.UseAuthorization();

// Liveness endpoint kept off any external dependency so it answers even if Postgres/
// Kafka are momentarily down.
application.MapGet("/healthz", () => Results.Ok(new { status = "ok", service = "identity" }));

application.MapControllers();

using (var serviceScope = application.Services.CreateScope())
{
    var startupLogger = serviceScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // database-per-service: make sure identity-db exists, then run our own migrations.
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
    catch (Exception ex)
    {
        startupLogger.LogWarning(ex, "Avatar storage init failed at startup; continuing without default avatars");
    }
}

application.Run();

// Exposed so the integration test host (WebApplicationFactory) can boot the service.
public partial class Program { }
