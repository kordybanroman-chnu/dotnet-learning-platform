using Catalog.Api;
using Catalog.Api.Grpc;
using Catalog.Api.Metrics;
using Catalog.Bll;
using Catalog.Bll.Caching;
using Catalog.Bll.Validators;
using Catalog.Dal;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddRedisDistributedCache("redis", settings => settings.DisableHealthChecks = true);
builder.Services.AddMemoryCache(options => options.SizeLimit = 10_000);
builder.Services.AddSingleton<CacheMetrics>();
builder.Services.AddScoped<ICatalogCache, TwoLevelCache>();
builder.Services.AddGrpc(options => options.Interceptors.Add<GrpcLoggingInterceptor>());

var connectionString = builder.Configuration.GetConnectionString("CatalogDb")
    ?? throw new InvalidOperationException("Connection string 'CatalogDb' is missing");
var redisConnectionString = builder.Configuration.GetConnectionString("redis")
    ?? throw new InvalidOperationException("Connection string 'redis' is missing");

builder.Services.AddHealthChecks()
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
builder.Services.AddGrpcHealthChecks(options =>
    options.Services.Map("", registration =>
        registration.Tags.Contains("ready") && !registration.Tags.Contains("cache")));

builder.Services.AddDbContext<CatalogDbContext>(options =>
    options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddSingleton<CatalogMetrics>();
builder.Services.AddHostedService<CatalogMetricsPoller>();
builder.Services.AddAutoMapper(configuration =>
{
    var licenseKey = builder.Configuration["AutoMapper:LicenseKey"];
    if (!string.IsNullOrWhiteSpace(licenseKey))
        configuration.LicenseKey = licenseKey;
    configuration.AddProfile<CourseProfile>();
});
builder.Services.AddValidatorsFromAssemblyContaining<CreateCourseDtoValidator>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
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

if (app.Configuration.GetValue("Catalog:MigrateAndSeed", true) && !app.Environment.IsProduction())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    await db.Database.MigrateAsync();
    await DbInitializer.SeedAsync(db);
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGrpcService<CatalogGrpcService>();
app.MapGrpcHealthChecksService();
app.MapDefaultEndpoints();

app.Run();
