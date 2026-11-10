using Grpc.Core;
using Grpc.Health.V1;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Platform.Aggregator.HealthChecks;

public abstract class DownstreamGrpcHealthCheck(
    GrpcClientFactory clientFactory,
    ILogger<DownstreamGrpcHealthCheck> logger) : IHealthCheck
{
    protected abstract string ClientName { get; }
    protected abstract string ServiceName { get; }
    protected virtual HealthStatus FailureStatus => HealthStatus.Unhealthy;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var client = clientFactory.CreateClient<Health.HealthClient>(ClientName);
            var reply = await client.CheckAsync(
                new HealthCheckRequest(),
                deadline: DateTime.UtcNow.AddSeconds(2),
                cancellationToken: ct);

            return reply.Status == HealthCheckResponse.Types.ServingStatus.Serving
                ? HealthCheckResult.Healthy($"{ServiceName} is serving")
                : new HealthCheckResult(FailureStatus, $"{ServiceName} status: {reply.Status}");
        }
        catch (RpcException ex)
        {
            logger.LogWarning(ex, "{ServiceName} health check failed", ServiceName);
            return new HealthCheckResult(FailureStatus, $"{ServiceName} is unavailable", ex);
        }
    }
}

public sealed class EnrollmentsHealthCheck(
    GrpcClientFactory clientFactory,
    ILogger<DownstreamGrpcHealthCheck> logger) : DownstreamGrpcHealthCheck(clientFactory, logger)
{
    protected override string ClientName => "enrollments-health";
    protected override string ServiceName => "enrollments-api";
}

public sealed class CatalogHealthCheck(
    GrpcClientFactory clientFactory,
    ILogger<DownstreamGrpcHealthCheck> logger) : DownstreamGrpcHealthCheck(clientFactory, logger)
{
    protected override string ClientName => "catalog-health";
    protected override string ServiceName => "catalog-api";
}

public sealed class ReviewsHealthCheck(
    GrpcClientFactory clientFactory,
    ILogger<DownstreamGrpcHealthCheck> logger) : DownstreamGrpcHealthCheck(clientFactory, logger)
{
    protected override string ClientName => "reviews-health";
    protected override string ServiceName => "reviews-api";
    protected override HealthStatus FailureStatus => HealthStatus.Degraded;
}
