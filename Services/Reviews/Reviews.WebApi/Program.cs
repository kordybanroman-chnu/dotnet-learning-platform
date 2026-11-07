using Reviews.Application;
using Reviews.Application.Reviews.Mapping;
using Reviews.Infrastructure;
using Reviews.Infrastructure.HealthChecks;
using Reviews.Infrastructure.Seeding;
using Platform.ServiceDefaults;
using Reviews.WebApi;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

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
builder.Services.AddHealthChecks()
    .AddCheck<MongoHealthCheck>("mongodb");
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
app.MapDefaultEndpoints();

app.Run();
