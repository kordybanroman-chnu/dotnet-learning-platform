using Microsoft.AspNetCore.Http;

namespace Platform.EventBus;

public interface ICorrelationAccessor
{
    string? CorrelationId { get; }
}

internal sealed class HttpCorrelationAccessor(IHttpContextAccessor accessor) : ICorrelationAccessor
{
    public const string HeaderName = "X-Correlation-Id";

    public string? CorrelationId
    {
        get
        {
            var value = accessor.HttpContext?.Request.Headers[HeaderName].ToString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
