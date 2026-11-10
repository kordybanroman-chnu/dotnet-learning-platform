using Microsoft.Extensions.Diagnostics.HealthChecks;
using Reviews.Infrastructure.Persistence;

namespace Reviews.WebApi.HealthChecks;

public static class ReviewsHealthChecks
{
    public static IHealthChecksBuilder AddReviewsHealthChecks(this IHealthChecksBuilder builder) =>
        builder.AddMongoDb(
            sp => sp.GetRequiredService<MongoDbContext>().Database,
            name: "reviews-db",
            failureStatus: HealthStatus.Unhealthy,
            tags: ["database", "mongodb", "ready", "startup"],
            timeout: TimeSpan.FromSeconds(5));
}
