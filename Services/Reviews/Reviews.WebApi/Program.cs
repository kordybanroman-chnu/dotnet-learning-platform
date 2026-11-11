using Reviews.Application;
using Reviews.Application.Reviews.Mapping;
using Reviews.Infrastructure;
using Reviews.Infrastructure.Seeding;
using Platform.EventBus;
using Platform.ServiceDefaults;
using Reviews.WebApi;
using Reviews.WebApi.Grpc;
using Reviews.WebApi.HealthChecks;
using Reviews.WebApi.IntegrationEvents;
using Reviews.WebApi.Metrics;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddEventBus();
builder.Services.AddGrpc(options => options.Interceptors.Add<GrpcLoggingInterceptor>());

_ = builder.Configuration.GetConnectionString("ReviewsMongo")
    ?? throw new InvalidOperationException("Connection string 'ReviewsMongo' is missing");

builder.Services
    .AddApplication(builder.Configuration)
    .AddInfrastructure(builder.Configuration);

builder.Services.AddAutoMapper(configuration =>
{
    var licenseKey = builder.Configuration["LuckyPenny:LicenseKey"];
    if (!string.IsNullOrWhiteSpace(licenseKey))
        configuration.LicenseKey = licenseKey;
    configuration.AddMaps(typeof(ReviewProfile).Assembly);
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<ReviewMetrics>();
builder.Services.AddHostedService<ReviewMetricsPoller>();
builder.Services.AddScoped<CourseRenamedHandler>();
builder.Services.AddHostedService<CatalogEventsConsumer>();
builder.Services.AddHealthChecks()
    .AddReviewsHealthChecks();
builder.Services.AddGrpcHealthChecks(options =>
    options.Services.Map("", registration =>
        registration.Tags.Contains("ready") && !registration.Tags.Contains("cache")));
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        var environment = context.HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        if (environment.IsDevelopment() && context.Exception is not null)
            context.ProblemDetails.Extensions["exception"] = context.Exception.Message;
    };
});
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();

if (app.Configuration.GetValue("Reviews:Seed", true) && !app.Environment.IsProduction())
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
    await seeder.SeedAsync();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGrpcService<ReviewsGrpcService>();
app.MapGrpcHealthChecksService();
app.MapDefaultEndpoints();

app.Run();
