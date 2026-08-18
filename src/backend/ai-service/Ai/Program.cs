using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Sellevate.Ai.Common.Constants;
using Sellevate.Ai.Common.Extensions;
using Sellevate.Ai.Eventing;
using Sellevate.Ai.Features.Companies;
using Sellevate.Ai.Features.ContentAdaptation;
using Sellevate.Ai.Features.ContentGeneration;
using Sellevate.Ai.Features.Dialog;
using Sellevate.Ai.Features.Dialog.Seeders;
using Sellevate.Ai.Features.Evaluation;
using Sellevate.Ai.Features.Quotas;
using Sellevate.Ai.Features.Transcription;
using Sellevate.Ai.Features.Voice;
using Sellevate.Ai.Infrastructure.Data;
using Sellevate.Ai.Infrastructure.HealthChecks;
using Sellevate.Ai.Infrastructure.Http;
using Sellevate.BuildingBlocks.DependencyInjection;
using Sellevate.BuildingBlocks.HealthChecks;
using Sellevate.BuildingBlocks.Messaging;
using Sellevate.BuildingBlocks.Tenancy;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using Serilog;
using Serilog.Sinks.Grafana.Loki;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) =>
{
    var lokiUrl = context.Configuration[AiConfigurationKeys.LokiUrl] ?? AiObservabilityDefaults.LokiUrl;

    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
        .WriteTo.GrafanaLoki(
            lokiUrl,
            labels:
            [
                new LokiLabel { Key = "service", Value = AiObservabilityDefaults.ServiceLabel },
                new LokiLabel { Key = "env",     Value = context.HostingEnvironment.EnvironmentName }
            ],
            propertiesAsLabels: ["RequestId", "UserId"])
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", AiObservabilityDefaults.ApplicationName);
});

BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

builder.Services.AddAiDataAccess(builder.Configuration);

builder.Services.AddSellevateEventing(builder.Configuration);
builder.Services.AddScoped<IDialogEventPublisher, KafkaDialogEventPublisher>();
builder.Services.AddSingleton<IDialogScoringWeightsProvider, DialogScoringWeightsProvider>();
builder.Services.AddHostedService<GamificationDialogWeightsConsumer>();
builder.Services.AddHostedService<UserReplicaConsumer>();
builder.Services.AddHostedService<OrganizationProfileConsumer>();

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

builder.Services.AddAiAuthentication(builder.Configuration);

builder.Services
    .AddDialogFeatureServices(builder.Configuration)
    .AddTranscriptionFeatureServices(builder.Configuration)
    .AddVoiceFeatureServices(builder.Configuration)
    .AddEvaluationFeatureServices()
    .AddCompanyAiFeatureServices()
    .AddContentGenerationFeatureServices()
    .AddContentAdaptationFeatureServices()
    .AddQuotaFeatureServices(builder.Configuration);

builder.Services.AddUpstreamHttpClients(builder.Configuration);

builder.Services.AddAiPresentation();

var application = builder.Build();

application.UseExceptionHandler();
application.UseSerilogRequestLogging();
application.UseHttpMetrics();
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
application.MapMetrics();

application.MapControllers();

using (var serviceScope = application.Services.CreateScope())
{
    var startupLogger = serviceScope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    await DatabaseBootstrapper.EnsureDatabaseExistsAsync(
        builder.Configuration.GetConnectionString(AiConfigurationKeys.PostgresConnectionStringName)!,
        startupLogger);

    var databaseContext = serviceScope.ServiceProvider.GetRequiredService<AiDbContext>();
    databaseContext.Database.Migrate();

    await CompanyCallModeSeeder.SeedAsync(databaseContext);
    await CustomScenarioModeSeeder.SeedAsync(databaseContext);
}

application.Run();

public partial class Program { }
