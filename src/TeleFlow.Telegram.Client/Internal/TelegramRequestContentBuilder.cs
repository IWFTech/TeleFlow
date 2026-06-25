using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Internal;

internal sealed class TelegramRequestContentBuilder
{
    private static readonly ConcurrentDictionary<Type, TypeMetadata> TypeMetadataCache = new();

    private readonly JsonSerializerOptions _serializerOptions;

    public TelegramRequestContentBuilder(JsonSerializerOptions serializerOptions)
    {
        ArgumentNullException.ThrowIfNull(serializerOptions);
        _serializerOptions = serializerOptions;
    }

    public TelegramTransportContent Build(object payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (!ContainsInputFile(payload, new HashSet<object>(ReferenceEqualityComparer.Instance)))
        {
            var json = JsonSerializer.Serialize(payload, payload.GetType(), _serializerOptions);
            return new TelegramJsonTransportContent(json);
        }

        return BuildMultipart(payload);
    }

    private TelegramMultipartTransportContent BuildMultipart(object payload)
    {
        var context = new MultipartBuildContext(_serializerOptions);

        foreach (var property in GetTypeMetadata(payload.GetType()).TelegramProperties)
        {
            var value = property.GetValue(payload);
            if (value is null)
            {
                continue;
            }

            var fieldName = property.TelegramName;
            if (TryGetDirectInputFile(value, out var inputFile))
            {
                context.AddFile(fieldName, inputFile);
                continue;
            }

            var node = context.ToJsonNode(value);
            if (node is null)
            {
                continue;
            }

            context.AddField(fieldName, node);
        }

        return context.Build();
    }

    private static bool ContainsInputFile(object? value, HashSet<object> visited)
    {
        if (value is null)
        {
            return false;
        }

        if (value is InputFile)
        {
            return true;
        }

        var type = value.GetType();
        var metadata = GetTypeMetadata(type);

        if (metadata.IsScalar || value is Stream)
        {
            return false;
        }

        if (!type.IsValueType && !visited.Add(value))
        {
            return false;
        }

        if (value is IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                if (ContainsInputFile(item, visited))
                {
                    return true;
                }
            }

            return false;
        }

        foreach (var property in metadata.ReadableProperties)
        {
            if (ContainsInputFile(property.GetValue(value), visited))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryGetDirectInputFile(object value, out InputFile inputFile)
    {
        if (value is InputFile direct)
        {
            inputFile = direct;
            return true;
        }

        var unionCase = GetActiveUnionCase(value);
        if (unionCase?.Value is InputFile nested)
        {
            inputFile = nested;
            return true;
        }

        inputFile = null!;
        return false;
    }

    private static ActiveUnionCase? GetActiveUnionCase(object value)
    {
        var metadata = GetTypeMetadata(value.GetType());
        if (metadata.TelegramProperties.Length > 0)
        {
            return null;
        }

        var activeCases = metadata.ReadableProperties
            .Select(property => new ActiveUnionCase(property.Name, property.GetValue(value)))
            .Where(static item => item.Value is not null)
            .ToArray();

        return activeCases.Length == 1 ? activeCases[0] : null;
    }

    private static TypeMetadata GetTypeMetadata(Type type)
    {
        return TypeMetadataCache.GetOrAdd(type, CreateTypeMetadata);
    }

    private static TypeMetadata CreateTypeMetadata(Type type)
    {
        var readableProperties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.CanRead && property.GetIndexParameters().Length == 0)
            .Select(static property => new PropertyMetadata(
                property,
                property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name))
            .ToArray();

        var telegramProperties = readableProperties
            .Where(static property => property.JsonPropertyName is not null)
            .ToArray();

        var scalarType = Nullable.GetUnderlyingType(type) ?? type;
        var isScalar = scalarType.IsPrimitive ||
                       scalarType.IsEnum ||
                       scalarType == typeof(string) ||
                       scalarType == typeof(decimal) ||
                       scalarType == typeof(DateTime) ||
                       scalarType == typeof(DateTimeOffset) ||
                       scalarType == typeof(Guid);

        return new TypeMetadata(isScalar, readableProperties, telegramProperties);
    }

    private sealed class MultipartBuildContext
    {
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly List<TelegramMultipartField> _fields = [];
        private readonly List<TelegramMultipartFile> _files = [];
        private int _fileIndex;

        public MultipartBuildContext(JsonSerializerOptions serializerOptions)
        {
            _serializerOptions = serializerOptions;
        }

        public TelegramMultipartTransportContent Build()
        {
            return new TelegramMultipartTransportContent(_fields.ToArray(), _files.ToArray());
        }

        public JsonNode? ToJsonNode(object? value)
        {
            if (value is null)
            {
                return null;
            }

            if (value is InputFile inputFile)
            {
                var fileName = "file" + _fileIndex.ToString(CultureInfo.InvariantCulture);
                _fileIndex++;
                AddFile(fileName, inputFile);
                return JsonValue.Create("attach://" + fileName);
            }

            if (value is string stringValue)
            {
                return JsonValue.Create(stringValue);
            }

            if (value is bool boolValue)
            {
                return JsonValue.Create(boolValue);
            }

            if (value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
            {
                return JsonSerializer.SerializeToNode(value, value.GetType(), _serializerOptions);
            }

            if (value is IEnumerable enumerable and not string)
            {
                var array = new JsonArray();
                foreach (var item in enumerable)
                {
                    array.Add(ToJsonNode(item));
                }

                return array;
            }

            var unionCase = GetActiveUnionCase(value);
            if (unionCase is not null)
            {
                return ToJsonNode(unionCase.Value);
            }

            var metadata = GetTypeMetadata(value.GetType());
            if (metadata.TelegramProperties.Length == 0)
            {
                return JsonSerializer.SerializeToNode(value, value.GetType(), _serializerOptions);
            }

            var jsonObject = new JsonObject();
            foreach (var property in metadata.TelegramProperties)
            {
                var propertyValue = property.GetValue(value);
                if (propertyValue is null)
                {
                    continue;
                }

                var node = ToJsonNode(propertyValue);
                if (node is not null)
                {
                    jsonObject[property.TelegramName] = node;
                }
            }

            return jsonObject;
        }

        public void AddField(string name, JsonNode node)
        {
            var value = node switch
            {
                JsonValue jsonValue when jsonValue.TryGetValue<string>(out var stringValue) => stringValue,
                JsonValue jsonValue when jsonValue.TryGetValue<bool>(out var boolValue) => boolValue ? "true" : "false",
                _ => node.ToJsonString(_serializerOptions)
            };

            _fields.Add(new TelegramMultipartField(name, value));
        }

        public void AddFile(string name, InputFile inputFile)
        {
            if (!inputFile.Content.CanRead)
            {
                throw new InvalidOperationException(
                    $"InputFile '{inputFile.FileName}' cannot be uploaded because its stream is not readable.");
            }

            if (!inputFile.Content.CanSeek)
            {
                throw new InvalidOperationException(
                    $"InputFile '{inputFile.FileName}' cannot be uploaded by the default Telegram executor because its stream is not seekable. Use a seekable stream so retry behavior can resend the same content.");
            }

            inputFile.Content.Position = 0;
            _files.Add(new TelegramMultipartFile(name, inputFile.FileName, inputFile.Content));
        }
    }

    private sealed record ActiveUnionCase(string Name, object? Value);

    private sealed record TypeMetadata(
        bool IsScalar,
        PropertyMetadata[] ReadableProperties,
        PropertyMetadata[] TelegramProperties);

    private sealed record PropertyMetadata(PropertyInfo Property, string? JsonPropertyName)
    {
        public string Name => Property.Name;

        public string TelegramName =>
            JsonPropertyName ??
            throw new InvalidOperationException(
                $"Telegram payload property '{Property.DeclaringType?.FullName}.{Property.Name}' is missing JsonPropertyName metadata.");

        public object? GetValue(object value)
        {
            return Property.GetValue(value);
        }
    }
}
