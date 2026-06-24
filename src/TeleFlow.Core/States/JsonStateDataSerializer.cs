using System.Text.Json;

namespace TeleFlow.Core.States;

public sealed class JsonStateDataSerializer : IStateDataSerializer
{
    private static readonly JsonSerializerOptions DefaultOptions = new(JsonSerializerDefaults.General);

    private readonly JsonSerializerOptions _options;

    public JsonStateDataSerializer()
        : this(DefaultOptions)
    {
    }

    public JsonStateDataSerializer(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public string Serialize<TValue>(TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return JsonSerializer.Serialize(value, _options);
    }

    public TValue? Deserialize<TValue>(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return JsonSerializer.Deserialize<TValue>(value, _options);
    }
}
