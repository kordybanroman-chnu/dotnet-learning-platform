using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Bson;
using Reviews.Infrastructure.Persistence;

namespace Reviews.Infrastructure.HealthChecks;

public sealed class MongoHealthCheck(MongoDbContext context) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext healthCheckContext, CancellationToken ct = default)
    {
        try
        {
            await context.Database.RunCommandAsync<BsonDocument>(
                new BsonDocument("ping", 1), cancellationToken: ct);
            return HealthCheckResult.Healthy("MongoDB responds to ping");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("MongoDB ping failed", exception);
        }
    }
}
