using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace Platform.ServiceDefaults;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].ToString();
        var generated = string.IsNullOrWhiteSpace(correlationId);
        if (generated)
        {
            correlationId = Guid.NewGuid().ToString();
            context.Request.Headers[HeaderName] = correlationId;
        }

        Activity.Current?.SetTag("correlation.id", correlationId);

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("RequestPath", context.Request.Path))
        {
            if (generated)
                logger.LogInformation("Generated CorrelationId {CorrelationId}", correlationId);

            logger.LogInformation("Request started {Method} {Path}",
                context.Request.Method, context.Request.Path);

            try
            {
                await next(context);
            }
            finally
            {
                logger.LogInformation("Request finished with {StatusCode}", context.Response.StatusCode);
            }
        }
    }
}

public sealed class CorrelationIdHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var correlationId = accessor.HttpContext?.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        if (!string.IsNullOrEmpty(correlationId) && !request.Headers.Contains(CorrelationIdMiddleware.HeaderName))
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);

        return base.SendAsync(request, cancellationToken);
    }
}
