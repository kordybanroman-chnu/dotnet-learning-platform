using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;

namespace Platform.Aggregator.Resilience;

public sealed class ResilienceLogger
{
    public ILogger? Logger { get; set; }

    public ValueTask CircuitOpened(OnCircuitOpenedArguments<HttpResponseMessage> args)
    {
        Logger?.LogError(
            "Circuit opened for {BreakDuration}, outcome {Outcome}",
            args.BreakDuration,
            args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
        return ValueTask.CompletedTask;
    }

    public ValueTask CircuitClosed(OnCircuitClosedArguments<HttpResponseMessage> args)
    {
        Logger?.LogInformation(
            "Circuit closed, outcome {Outcome}",
            args.Outcome.Exception?.Message ?? args.Outcome.Result?.StatusCode.ToString());
        return ValueTask.CompletedTask;
    }

    public ValueTask CircuitHalfOpened(OnCircuitHalfOpenedArguments args)
    {
        Logger?.LogWarning("Circuit half-opened, probing the dependency");
        return ValueTask.CompletedTask;
    }
}
