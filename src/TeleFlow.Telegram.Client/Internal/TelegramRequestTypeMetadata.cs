using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;

namespace TeleFlow.Telegram.Internal;

/// <summary>
/// Caches reflection metadata used by Telegram request content building.
/// The cache keeps JSON property discovery, ignored-property handling, and scalar detection in one place.
/// It is shared by JSON/multipart routing and multipart rendering so both paths interpret generated schema types consistently.
/// </summary>
internal static class TelegramRequestTypeMetadataCache
{
    private static readonly ConcurrentDictionary<Type, TelegramRequestTypeMetadata> MetadataCache = new();

    public static TelegramRequestTypeMetadata Get(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return MetadataCache.GetOrAdd(type, Create);
    }

    public static bool IsScalarType(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var scalarType = Nullable.GetUnderlyingType(type) ?? type;

        return scalarType.IsPrimitive ||
               scalarType.IsEnum ||
               scalarType == typeof(string) ||
               scalarType == typeof(decimal) ||
               scalarType == typeof(DateTime) ||
               scalarType == typeof(DateTimeOffset) ||
               scalarType == typeof(Guid);
    }

    private static TelegramRequestTypeMetadata Create(Type type)
    {
        var readableProperties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.CanRead && property.GetIndexParameters().Length == 0)
            // Keep reflection metadata aligned with System.Text.Json and avoid evaluating ignored getters
            // while deciding whether a request needs multipart.
            .Where(static property => !IsAlwaysJsonIgnored(property))
            .Select(static property => new TelegramRequestPropertyMetadata(
                property,
                property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name))
            .ToArray();

        var telegramProperties = readableProperties
            .Where(static property => property.JsonPropertyName is not null)
            .ToArray();

        return new TelegramRequestTypeMetadata(IsScalarType(type), readableProperties, telegramProperties);
    }

    private static bool IsAlwaysJsonIgnored(PropertyInfo property)
    {
        var ignore = property.GetCustomAttribute<JsonIgnoreAttribute>();
        return ignore?.Condition == JsonIgnoreCondition.Always;
    }
}

/// <summary>
/// Reflection snapshot for one request-related CLR type.
/// It separates all readable properties from Telegram JSON fields discovered through JsonPropertyName metadata.
/// </summary>
internal sealed record TelegramRequestTypeMetadata(
    bool IsScalar,
    TelegramRequestPropertyMetadata[] ReadableProperties,
    TelegramRequestPropertyMetadata[] TelegramProperties);

/// <summary>
/// Reflection wrapper around a readable CLR property and its optional Telegram JSON field name.
/// Multipart rendering uses it to read CLR values and write Telegram field names without repeating reflection rules.
/// </summary>
internal sealed record TelegramRequestPropertyMetadata(PropertyInfo Property, string? JsonPropertyName)
{
    public Type PropertyType => Property.PropertyType;

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
