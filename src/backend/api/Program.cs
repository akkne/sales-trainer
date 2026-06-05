using System.Text;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using Prometheus;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using SalesTrainer.Api.Infrastructure.Data;
using SalesTrainer.Api.Features.Achievements;
using SalesTrainer.Api.Features.Auth;
using SalesTrainer.Api.Features.Dialog;
using SalesTrainer.Api.Features.Exercises;
using SalesTrainer.Api.Features.Friends;
using SalesTrainer.Api.Features.Gamification;
using SalesTrainer.Api.Features.League;
using SalesTrainer.Api.Features.Notifications;
using SalesTrainer.Api.Features.Onboarding;
using SalesTrainer.Api.Features.Profile;
using SalesTrainer.Api.Features.Reference;
using SalesTrainer.Api.Features.SkillTree;
using SalesTrainer.Api.Features.Techniques;
using SalesTrainer.Api.Features.Transcription;
using SalesTrainer.Api.Features.Voice;
using StackExchange.Redis;

BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

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
                new LokiLabel { Key = "service", Value = "sellevate-backend" },
                new LokiLabel { Key = "env",     Value = context.HostingEnvironment.EnvironmentName }
            ],
            propertiesAsLabels: ["RequestId", "UserId"]
        )
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "Sellevate.Api");
});

builder.Services.AddDbContext<AppDbContext>(databaseOptions =>
    databaseOptions.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddSingleton<IMongoClient>(_ =>
    new MongoClient(builder.Configuration.GetConnectionString("Mongo")));

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

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

builder.Services.AddHangfire(hangfireConfiguration =>
    hangfireConfiguration.UsePostgreSqlStorage(storageOptions =>
        storageOptions.UseNpgsqlConnection(builder.Configuration.GetConnectionString("Postgres"))));
builder.Services.AddHangfireServer();

builder.Services.AddCors(corsOptions => corsOptions.AddDefaultPolicy(corsPolicy =>
    corsPolicy
        .WithOrigins(builder.Configuration["Frontend:Url"] ?? "http://localhost:3000")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

builder.Services
    .AddAchievementFeatureServices()
    .AddAuthenticationFeatureServices()
    .AddDialogFeatureServices()
    .AddExerciseFeatureServices()
    .AddFriendFeatureServices()
    .AddGamificationFeatureServices()
    .AddLeagueFeatureServices()
    .AddNotificationFeatureServices()
    .AddOnboardingFeatureServices()
    .AddProfileFeatureServices()
    .AddReferenceFeatureServices()
    .AddSkillTreeFeatureServices()
    .AddTechniqueFeatureServices()
    .AddTranscriptionFeatureServices()
    .AddVoiceFeatureServices(builder.Configuration);

builder.Services.AddHttpClient("OpenAI")
    .ConfigureHttpClient(client =>
        client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient("VoicerTts")
    .ConfigureHttpClient(client =>
        client.Timeout = TimeSpan.FromSeconds(120));
builder.Services.AddHttpClient("GoogleTts")
    .ConfigureHttpClient(client =>
        client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient("YandexTts")
    .ConfigureHttpClient(client =>
        client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var application = builder.Build();

application.UseSerilogRequestLogging();
application.UseCors();
application.UseHttpMetrics();
application.MapMetrics();

if (application.Environment.IsDevelopment())
{
    application.UseSwagger();
    application.UseSwaggerUI();
}

application.UseAuthentication();
application.UseAuthorization();
application.UseHangfireDashboard("/hangfire");
application.MapControllers();

RecurringJob.AddOrUpdate<WeeklyLeagueClosureJob>(
    "weekly-league-closure",
    weeklyLeagueClosureJob => weeklyLeagueClosureJob.ExecuteAsync(),
    "0 0 * * 1");

RecurringJob.AddOrUpdate<StreakResetJob>(
    "daily-streak-reset",
    streakResetJob => streakResetJob.ExecuteAsync(),
    "5 0 * * *");

RecurringJob.AddOrUpdate<NotificationCleanupJob>(
    "notification-cleanup",
    notificationCleanupJob => notificationCleanupJob.ExecuteAsync(),
    "30 0 * * *");

using (var serviceScope = application.Services.CreateScope())
{
    var databaseContext = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();
    databaseContext.Database.Migrate();

    var superAdminSeeder = serviceScope.ServiceProvider.GetRequiredService<SuperAdminSeeder>();
    await superAdminSeeder.SeedAsync();

    var achievementSeeder = serviceScope.ServiceProvider.GetRequiredService<AchievementSeeder>();
    await achievementSeeder.SeedAsync();

    var dialogSeeder = serviceScope.ServiceProvider.GetRequiredService<DialogSeeder>();
    await dialogSeeder.SeedAsync();
}

application.Run();

public partial class Program { }
