using System.Text;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using Serilog;
using SalesTrainer.Api.Infrastructure.Data;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((hostingContext, loggerConfiguration) =>
    loggerConfiguration.ReadFrom.Configuration(hostingContext.Configuration).WriteTo.Console());

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

builder.Services.AddAuthorization();

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
builder.Services.AddScoped<SalesTrainer.Api.Features.Onboarding.OnboardingService>();
builder.Services.AddScoped<SalesTrainer.Api.Features.SkillTree.SkillTreeService>();
builder.Services.AddScoped<SalesTrainer.Api.Features.Exercises.ExerciseService>();
builder.Services.AddScoped<SalesTrainer.Api.Features.Exercises.ExerciseEvaluationFactory>();
builder.Services.AddScoped<SalesTrainer.Api.Features.Exercises.IExerciseEvaluationStrategy,
    SalesTrainer.Api.Features.Exercises.MultipleChoiceEvaluationStrategy>();
builder.Services.AddScoped<SalesTrainer.Api.Features.Exercises.IExerciseEvaluationStrategy,
    SalesTrainer.Api.Features.Exercises.FillBlankEvaluationStrategy>();
builder.Services.AddScoped<SalesTrainer.Api.Features.Exercises.IExerciseEvaluationStrategy,
    SalesTrainer.Api.Features.Exercises.FreeTextEvaluationStrategy>();
builder.Services.AddHttpClient("OpenAI")
    .ConfigureHttpClient(client =>
        client.Timeout = TimeSpan.FromSeconds(30));

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
application.UseHangfireDashboard("/hangfire");
application.MapControllers();

using (var serviceScope = application.Services.CreateScope())
{
    var databaseContext = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();
    databaseContext.Database.Migrate();
}

application.Run();
