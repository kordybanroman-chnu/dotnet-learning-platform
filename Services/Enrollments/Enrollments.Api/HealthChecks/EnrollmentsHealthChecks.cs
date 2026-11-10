using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Enrollments.Api.HealthChecks;

public static class EnrollmentsHealthChecks
{
    public static IHealthChecksBuilder AddEnrollmentsHealthChecks(
        this IHealthChecksBuilder builder, string connectionString) =>
        builder.AddSqlServer(
            connectionString: connectionString,
            name: "enrollments-db",
            failureStatus: HealthStatus.Unhealthy,
            tags: ["database", "sql", "ready", "startup"],
            timeout: TimeSpan.FromSeconds(5));
}
