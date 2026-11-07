using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace Platform.ServiceDefaults;

public sealed class GrpcLoggingInterceptor(ILogger<GrpcLoggingInterceptor> logger) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = context.RequestHeaders.GetValue("x-correlation-id");
        IDisposable? scope = string.IsNullOrEmpty(correlationId)
            ? null
            : LogContext.PushProperty("CorrelationId", correlationId);

        using (scope)
        {
            try
            {
                var response = await continuation(request, context);
                stopwatch.Stop();
                logger.LogInformation(
                    "gRPC {Method} from {Peer} finished with {StatusCode} in {ElapsedMilliseconds} ms",
                    context.Method, context.Peer, StatusCode.OK, stopwatch.ElapsedMilliseconds);
                return response;
            }
            catch (RpcException ex)
            {
                stopwatch.Stop();
                logger.LogWarning(ex,
                    "gRPC {Method} from {Peer} finished with {StatusCode} in {ElapsedMilliseconds} ms",
                    context.Method, context.Peer, ex.StatusCode, stopwatch.ElapsedMilliseconds);
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                logger.LogError(ex,
                    "gRPC {Method} from {Peer} failed in {ElapsedMilliseconds} ms",
                    context.Method, context.Peer, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }
}
