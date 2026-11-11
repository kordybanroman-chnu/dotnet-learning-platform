using System.Diagnostics;
using Platform.ServiceDefaults;
using Yarp.ReverseProxy.Model;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();

app.Use(async (context, next) =>
{
    var stopwatch = Stopwatch.StartNew();
    try
    {
        await next();
    }
    finally
    {
        stopwatch.Stop();
        var route = context.Features.Get<IReverseProxyFeature>()?.Route.Config.RouteId ?? "-";
        app.Logger.LogInformation(
            "{Method} {Path} from {ClientIp} -> route {Route}: {StatusCode} in {ElapsedMilliseconds} ms",
            context.Request.Method, context.Request.Path, context.Connection.RemoteIpAddress,
            route, context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
    }
});

app.MapReverseProxy();
app.MapDefaultEndpoints();

app.Run();
