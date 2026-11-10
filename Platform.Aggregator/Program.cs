using System.Text.Json.Serialization;
using Catalog.Grpc;
using Enrollments.Grpc;
using Microsoft.Extensions.Logging;
using Platform.Aggregator.Resilience;
using Platform.Aggregator.Services;
using Platform.ServiceDefaults;
using Reviews.Grpc;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddRedisDistributedCache("redis");

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
