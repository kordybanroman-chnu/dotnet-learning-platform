using System.Text.Json;
using System.Text.Json.Serialization;

namespace Platform.EventBus;

public static class EventBusJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
