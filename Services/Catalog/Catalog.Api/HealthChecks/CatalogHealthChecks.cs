using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Catalog.Api.HealthChecks;

public static class CatalogHealthChecks
{
    public static IHealthChecksBuilder AddCatalogHealthChecks(
        this IHealthChecksBuilder builder, string connectionString, string redisConnectionString) =>
        builder
            .AddSqlServer(
                connectionString: connectionString,
                name: "catalog-db",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["database", "sql", "ready", "startup"],
                timeout: TimeSpan.FromSeconds(5))
            .AddRedis(
                redisConnectionString: redisConnectionString,
                name: "redis-cache",
                failureStatus: HealthStatus.Degraded,
                tags: ["cache", "redis", "ready"],
                timeout: TimeSpan.FromSeconds(3));
}
