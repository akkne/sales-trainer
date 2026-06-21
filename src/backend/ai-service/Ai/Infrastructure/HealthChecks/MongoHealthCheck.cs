using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Sellevate.Ai.Infrastructure.HealthChecks;

/// <summary>
/// Readiness check that confirms MongoDB is reachable by running the <c>ping</c> admin
/// command. Reports <see cref="HealthStatus.Unhealthy"/> when the server cannot be reached.
/// </summary>
internal sealed class MongoHealthCheck : IHealthCheck
{
    private readonly IMongoClient _mongoClient;

    public MongoHealthCheck(IMongoClient mongoClient)
    {
        _mongoClient = mongoClient;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var command = new BsonDocument("ping", 1);
            await _mongoClient.GetDatabase("admin").RunCommandAsync<BsonDocument>(command, cancellationToken: cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("MongoDB is not reachable.", exception);
        }
    }
}
