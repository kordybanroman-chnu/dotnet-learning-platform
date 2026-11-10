using System.Text.Json.Serialization;
using Catalog.Grpc;
using Enrollments.Grpc;
using Platform.Aggregator.Services;
using Platform.ServiceDefaults;
using Reviews.Grpc;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddRedisDistributedCache("redis");

builder.Services.AddGrpcClient<EnrollmentsService.EnrollmentsServiceClient>(options =>
        options.Address = new Uri("https://enrollments-api"))
    .ConfigurePrimaryHttpMessageHandler(DevelopmentCertificateHandler);

builder.Services.AddGrpcClient<CatalogService.CatalogServiceClient>(options =>
        options.Address = new Uri("https://catalog-api"))
    .ConfigurePrimaryHttpMessageHandler(DevelopmentCertificateHandler);

builder.Services.AddGrpcClient<ReviewsService.ReviewsServiceClient>(options =>
        options.Address = new Uri("https://reviews-api"))
    .ConfigurePrimaryHttpMessageHandler(DevelopmentCertificateHandler);

builder.Services.AddScoped<EnrollmentAggregator>();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

var app = builder.Build();

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
