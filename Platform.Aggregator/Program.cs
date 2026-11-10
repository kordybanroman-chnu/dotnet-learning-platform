using System.Text.Json.Serialization;
using Catalog.Grpc;
using Enrollments.Grpc;
using Grpc.Health.V1;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Platform.Aggregator.HealthChecks;
using Platform.Aggregator.Resilience;
using Platform.Aggregator.Services;
using Platform.ServiceDefaults;
using Reviews.Grpc;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddRedisDistributedCache("redis", settings => settings.DisableHealthChecks = true);

var resilienceLogger = new ResilienceLogger();

var enrollmentsClient = builder.Services
    .AddGrpcClient<EnrollmentsService.EnrollmentsServiceClient>(options =>
        options.Address = new Uri("https://enrollments-api"))
    .ConfigureChannel(channel => channel.ServiceConfig = GrpcResilience.EnrollmentsChannel)
    .ConfigurePrimaryHttpMessageHandler(DevelopmentCertificateHandler);
GrpcResilience.ConfigureEnrollments(enrollmentsClient, resilienceLogger);

var catalogClient = builder.Services
    .AddGrpcClient<CatalogService.CatalogServiceClient>(options =>
        options.Address = new Uri("https://catalog-api"))
    .ConfigureChannel(channel => channel.ServiceConfig = GrpcResilience.CatalogChannel)
    .ConfigurePrimaryHttpMessageHandler(DevelopmentCertificateHandler);
GrpcResilience.ConfigureCatalog(catalogClient, resilienceLogger);

var reviewsClient = builder.Services
    .AddGrpcClient<ReviewsService.ReviewsServiceClient>(options =>
        options.Address = new Uri("https://reviews-api"))
    .ConfigureChannel(channel => channel.ServiceConfig = GrpcResilience.ReviewsChannel)
    .ConfigurePrimaryHttpMessageHandler(DevelopmentCertificateHandler);
GrpcResilience.ConfigureReviews(reviewsClient, resilienceLogger);

var enrollmentsHealthClient = builder.Services
    .AddGrpcClient<Health.HealthClient>("enrollments-health", options =>
        options.Address = new Uri("https://enrollments-api"))
    .ConfigurePrimaryHttpMessageHandler(DevelopmentCertificateHandler);
GrpcResilience.RemoveResilience(enrollmentsHealthClient);

var catalogHealthClient = builder.Services
    .AddGrpcClient<Health.HealthClient>("catalog-health", options =>
        options.Address = new Uri("https://catalog-api"))
    .ConfigurePrimaryHttpMessageHandler(DevelopmentCertificateHandler);
GrpcResilience.RemoveResilience(catalogHealthClient);

var reviewsHealthClient = builder.Services
    .AddGrpcClient<Health.HealthClient>("reviews-health", options =>
        options.Address = new Uri("https://reviews-api"))
    .ConfigurePrimaryHttpMessageHandler(DevelopmentCertificateHandler);
GrpcResilience.RemoveResilience(reviewsHealthClient);

var redisConnectionString = builder.Configuration.GetConnectionString("redis")
    ?? throw new InvalidOperationException("Connection string 'redis' is missing");

builder.Services.AddHealthChecks()
    .AddRedis(
        redisConnectionString: redisConnectionString,
        name: "redis-cache",
        failureStatus: HealthStatus.Degraded,
        tags: ["cache", "redis", "ready"],
        timeout: TimeSpan.FromSeconds(3))
    .AddCheck<EnrollmentsHealthCheck>(
        "enrollments-api",
        tags: ["grpc", "downstream", "ready"],
        timeout: TimeSpan.FromSeconds(3))
    .AddCheck<CatalogHealthCheck>(
        "catalog-api",
        tags: ["grpc", "downstream", "ready"],
        timeout: TimeSpan.FromSeconds(3))
    .AddCheck<ReviewsHealthCheck>(
        "reviews-api",
        tags: ["grpc", "downstream", "ready"],
        timeout: TimeSpan.FromSeconds(3));

builder.Services.AddScoped<EnrollmentAggregator>();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

var app = builder.Build();

resilienceLogger.Logger = app.Services.GetRequiredService<ILoggerFactory>()
    .CreateLogger("Aggregator.Resilience");

app.UseMiddleware<CorrelationIdMiddleware>();
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();

HttpClientHandler DevelopmentCertificateHandler() => new()
{
    ServerCertificateCustomValidationCallback = builder.Environment.IsDevelopment()
        ? HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        : null
};
