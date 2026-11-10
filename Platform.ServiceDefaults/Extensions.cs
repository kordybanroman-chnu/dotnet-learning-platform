using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.ServiceDiscovery;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Platform.ServiceDefaults;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace Microsoft.Extensions.Hosting;

public static class Extensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddSerilog((_, configuration) => configuration
            .MinimumLevel.Is(builder.Environment.IsDevelopment()
                ? LogEventLevel.Debug
                : LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Information)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware", LogEventLevel.Fatal)
            .MinimumLevel.Override("LuckyPennySoftware.AutoMapper.License", LogEventLevel.Fatal)
            .MinimumLevel.Override("LuckyPennySoftware.MediatR.License", LogEventLevel.Fatal)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("ServiceName", builder.Environment.ApplicationName)
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
            .Enrich.WithEnvironmentName()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithSpan()
            .WriteTo.Console(new CompactJsonFormatter()),
            writeToProviders: true);

        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<CorrelationIdHandler>();
        builder.Services.AddSingleton<GrpcLoggingInterceptor>();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler(options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
                options.Retry.DisableForUnsafeHttpMethods();
            });
            http.AddServiceDiscovery();
            http.AddHttpMessageHandler<CorrelationIdHandler>();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        var samplingRatio = builder.Configuration.GetValue(
            "OpenTelemetry:SamplingRatio", builder.Environment.IsDevelopment() ? 1.0 : 0.1);

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter("Catalog.Cache", "Enrollments.Api", "Catalog.Api", "Reviews.WebApi", "Polly");
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddSource("Enrollments.Api")
                    .AddSource("Catalog.Api")
                    .AddSource("Reviews.WebApi")
                    .AddSource("Grpc.Net.Client")
                    .AddSource("Grpc.AspNetCore.Server")
                    .AddSource("MongoDB.Driver.Core.Extensions.DiagnosticSources")
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddSqlClientInstrumentation()
                    .SetSampler(new TraceIdRatioBasedSampler(samplingRatio));
            });

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            builder.Services.AddOpenTelemetry().UseOtlpExporter();

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteHealthResponse
        });

        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("live")
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("ready"),
            ResponseWriter = WriteHealthResponse
        });

        app.MapHealthChecks("/health/startup", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("startup"),
            ResponseWriter = WriteHealthResponse
        });

        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains("live")
        });

        return app;
    }

    private static Task WriteHealthResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        return context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 2),
                description = entry.Value.Description,
                exception = entry.Value.Exception?.Message
            })
        });
    }
}
