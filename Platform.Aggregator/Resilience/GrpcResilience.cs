using Grpc.Core;
using Grpc.Net.Client.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Platform.Aggregator.Resilience;

public static class GrpcResilience
{
    private static readonly ResilienceProfile Enrollments = new(
        MaxRetryAttempts: 2,
        RetryDelay: TimeSpan.FromSeconds(0.5),
        AttemptTimeout: TimeSpan.FromSeconds(5),
        TotalTimeout: TimeSpan.FromSeconds(20),
        FailureRatio: 0.5,
        MinimumThroughput: 10,
        SamplingDuration: TimeSpan.FromSeconds(30),
        BreakDuration: TimeSpan.FromSeconds(10));

    private static readonly ResilienceProfile Catalog = new(
        MaxRetryAttempts: 2,
        RetryDelay: TimeSpan.FromSeconds(0.5),
        AttemptTimeout: TimeSpan.FromSeconds(6),
        TotalTimeout: TimeSpan.FromSeconds(25),
        FailureRatio: 0.3,
        MinimumThroughput: 10,
        SamplingDuration: TimeSpan.FromSeconds(30),
        BreakDuration: TimeSpan.FromSeconds(15));

    private static readonly ResilienceProfile Reviews = new(
        MaxRetryAttempts: 1,
        RetryDelay: TimeSpan.FromSeconds(0.5),
        AttemptTimeout: TimeSpan.FromSeconds(2),
        TotalTimeout: TimeSpan.FromSeconds(6),
        FailureRatio: 0.7,
        MinimumThroughput: 5,
        SamplingDuration: TimeSpan.FromSeconds(30),
        BreakDuration: TimeSpan.FromSeconds(5));

    public static ServiceConfig EnrollmentsChannel { get; } = BuildServiceConfig(
        maxAttempts: 3,
        maxBackoff: TimeSpan.FromSeconds(1),
        StatusCode.Unavailable);

    public static ServiceConfig CatalogChannel { get; } = BuildServiceConfig(
        maxAttempts: 4,
        maxBackoff: TimeSpan.FromSeconds(2),
        StatusCode.Unavailable,
        StatusCode.ResourceExhausted);

    public static ServiceConfig ReviewsChannel { get; } = BuildServiceConfig(
        maxAttempts: 2,
        maxBackoff: TimeSpan.FromSeconds(1),
        StatusCode.Unavailable);

    public static void ConfigureEnrollments(IHttpClientBuilder client, ResilienceLogger logger) =>
        Configure(client, Enrollments, logger);

    public static void ConfigureCatalog(IHttpClientBuilder client, ResilienceLogger logger) =>
        Configure(client, Catalog, logger);

    public static void ConfigureReviews(IHttpClientBuilder client, ResilienceLogger logger) =>
        Configure(client, Reviews, logger);

    public static void RemoveResilience(IHttpClientBuilder client)
    {
#pragma warning disable EXTEXP0001
        client.RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001
    }

    private static void Configure(
        IHttpClientBuilder client, ResilienceProfile profile, ResilienceLogger logger)
    {
#pragma warning disable EXTEXP0001
        client.RemoveAllResilienceHandlers()
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = profile.MaxRetryAttempts;
                options.Retry.Delay = profile.RetryDelay;
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;
                options.AttemptTimeout.Timeout = profile.AttemptTimeout;
                options.TotalRequestTimeout.Timeout = profile.TotalTimeout;
                options.CircuitBreaker.FailureRatio = profile.FailureRatio;
                options.CircuitBreaker.MinimumThroughput = profile.MinimumThroughput;
                options.CircuitBreaker.SamplingDuration = profile.SamplingDuration;
                options.CircuitBreaker.BreakDuration = profile.BreakDuration;
                options.CircuitBreaker.OnOpened = logger.CircuitOpened;
                options.CircuitBreaker.OnClosed = logger.CircuitClosed;
                options.CircuitBreaker.OnHalfOpened = logger.CircuitHalfOpened;
            });
#pragma warning restore EXTEXP0001
    }

    private static ServiceConfig BuildServiceConfig(
        int maxAttempts, TimeSpan maxBackoff, params StatusCode[] retryableStatusCodes)
    {
        var policy = new RetryPolicy
        {
            MaxAttempts = maxAttempts,
            InitialBackoff = TimeSpan.FromSeconds(0.5),
            MaxBackoff = maxBackoff,
            BackoffMultiplier = 2
        };

        foreach (var statusCode in retryableStatusCodes)
            policy.RetryableStatusCodes.Add(statusCode);

        return new ServiceConfig
        {
            MethodConfigs =
            {
                new MethodConfig
                {
                    Names = { MethodName.Default },
                    RetryPolicy = policy
                }
            }
        };
    }

    private sealed record ResilienceProfile(
        int MaxRetryAttempts,
        TimeSpan RetryDelay,
        TimeSpan AttemptTimeout,
        TimeSpan TotalTimeout,
        double FailureRatio,
        int MinimumThroughput,
        TimeSpan SamplingDuration,
        TimeSpan BreakDuration);
}
