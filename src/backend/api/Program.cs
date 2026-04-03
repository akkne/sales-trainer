using System.Text;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Prometheus;
using Serilog;
using Serilog.Sinks.Grafana.Loki;
using SalesTrainer.Api.Infrastructure.Data;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, loggerConfig) =>
{
    var lokiUrl = ctx.Configuration["Logging:Loki:Url"] ?? "http://loki:3100";

    loggerConfig
        .ReadFrom.Configuration(ctx.Configuration)
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
        .WriteTo.GrafanaLoki(
            lokiUrl,
            labels:
            [
                new LokiLabel { Key = "service", Value = "salestrainer-backend" },
                new LokiLabel { Key = "env",     Value = ctx.HostingEnvironment.EnvironmentName }
            ],
            propertiesAsLabels: ["RequestId", "UserId"]
        )
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "SalesTrainer.Api");
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

builder.Services.AddAuthorization(authOptions =>
{
    authOptions.AddPolicy("RequireAdmin", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.IsInRole("Admin") || ctx.User.IsInRole("SuperAdmin")));
    authOptions.AddPolicy("RequireSuperAdmin", policy =>
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

builder.Services.AddScoped<SalesTrainer.Api.Features.Auth.AuthenticationService>();
builder.Services.AddScoped<SalesTrainer.Api.Features.Auth.SuperAdminSeeder>();
builder.Services.AddScoped<SalesTrainer.Api.Features.Onboarding.OnboardingService>();
builder.Services.AddScoped<SalesTrainer.Api.Features.SkillTree.SkillTreeService>();
builder.Services.AddScoped<SalesTrainer.Api.Features.Exercises.ExerciseService>();
builder.Services.AddScoped<SalesTrainer.Api.Features.Reference.ReferenceService>();
builder.Services.AddScoped<SalesTrainer.Api.Features.Profile.ProfileService>();
builder.Services.AddScoped<SalesTrainer.Api.Features.League.LeagueService>();
builder.Services.AddScoped<SalesTrainer.Api.Features.League.WeeklyLeagueClosureJob>();
builder.Services.AddScoped<SalesTrainer.Api.Features.Gamification.StreakResetJob>();
builder.Services.AddScoped<SalesTrainer.Api.Features.Exercises.ExerciseEvaluationFactory>();
builder.Services.AddScoped<SalesTrainer.Api.Features.Transcription.ITranscriptionService,
    SalesTrainer.Api.Features.Transcription.WhisperTranscriptionService>();
builder.Services.AddScoped<SalesTrainer.Api.Features.Exercises.IExerciseEvaluationStrategy,
    SalesTrainer.Api.Features.Exercises.MultipleChoiceEvaluationStrategy>();
builder.Services.AddScoped<SalesTrainer.Api.Features.Exercises.IExerciseEvaluationStrategy,
    SalesTrainer.Api.Features.Exercises.FillBlankEvaluationStrategy>();
builder.Services.AddScoped<SalesTrainer.Api.Features.Exercises.IExerciseEvaluationStrategy,
    SalesTrainer.Api.Features.Exercises.FreeTextEvaluationStrategy>();
builder.Services.AddHttpClient("OpenAI")
    .ConfigureHttpClient(client =>
        client.Timeout = TimeSpan.FromSeconds(30));
builder.Services.AddHttpClient(); // fallback default client

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

RecurringJob.AddOrUpdate<SalesTrainer.Api.Features.League.WeeklyLeagueClosureJob>(
    "weekly-league-closure",
    weeklyLeagueClosureJob => weeklyLeagueClosureJob.ExecuteAsync(),
    "0 0 * * 1");

RecurringJob.AddOrUpdate<SalesTrainer.Api.Features.Gamification.StreakResetJob>(
    "daily-streak-reset",
    streakResetJob => streakResetJob.ExecuteAsync(),
    "5 0 * * *"); // 00:05 UTC every day

using (var serviceScope = application.Services.CreateScope())
{
    var databaseContext = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();
    databaseContext.Database.Migrate();

    var superAdminSeeder = serviceScope.ServiceProvider.GetRequiredService<SalesTrainer.Api.Features.Auth.SuperAdminSeeder>();
    await superAdminSeeder.SeedAsync();
}

application.Run();

public partial class Program { }
