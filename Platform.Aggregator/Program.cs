using System.Text.Json.Serialization;
using Platform.Aggregator.Clients;
using Platform.Aggregator.Services;
using Platform.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddHttpClient<EnrollmentsClient>(client =>
    client.BaseAddress = new("https+http://enrollments-api"));
builder.Services.AddHttpClient<CatalogClient>(client =>
    client.BaseAddress = new("https+http://catalog-api"));
builder.Services.AddHttpClient<ReviewsClient>(client =>
    client.BaseAddress = new("https+http://reviews-api"));

builder.Services.AddScoped<EnrollmentAggregator>();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
