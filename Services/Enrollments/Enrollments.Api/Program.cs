using Enrollments.Api;
using Enrollments.Api.Grpc;
using Enrollments.Api.Metrics;
using Enrollments.Bll;
using Enrollments.Dal;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddGrpc(options => options.Interceptors.Add<GrpcLoggingInterceptor>());

var connectionString = builder.Configuration.GetConnectionString("EnrollmentsDb")
    ?? throw new InvalidOperationException("Connection string 'EnrollmentsDb' is missing");

builder.Services.AddHealthChecks()
    .AddSqlServer(
        connectionString: connectionString,
        name: "enrollments-db",
        failureStatus: HealthStatus.Unhealthy,
        tags: ["database", "sql", "ready", "startup"],
        timeout: TimeSpan.FromSeconds(5));
builder.Services.AddGrpcHealthChecks(options =>
    options.Services.Map("", registration =>
        registration.Tags.Contains("ready") && !registration.Tags.Contains("cache")));

builder.Services.AddScoped<IUnitOfWork>(_ => new UnitOfWork(connectionString));
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddSingleton<EnrollmentMetrics>();
builder.Services.AddHostedService<EnrollmentMetricsPoller>();
builder.Services.AddAutoMapper(cfg =>
{
    var licenseKey = builder.Configuration["AutoMapper:LicenseKey"];
    if (!string.IsNullOrWhiteSpace(licenseKey))
        cfg.LicenseKey = licenseKey;
    cfg.AddProfile<EnrollmentProfile>();
});
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails(o =>
    o.CustomizeProblemDetails = ctx =>
        ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddExceptionHandler<DomainExceptionHandler>();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.MapGrpcService<EnrollmentsGrpcService>();
app.MapGrpcHealthChecksService();
app.MapDefaultEndpoints();

app.Run();
