using System.Globalization;
using System.Reflection;
using TeleFlow.Annotations;

namespace TeleFlow.Telegram.Internal;

internal sealed class CallbackDataMetadata
{
    private CallbackDataMetadata(
        Type payloadType,
        string prefix,
        IReadOnlyList<CallbackDataField> fields,
        ConstructorInfo? constructor)
    {
        PayloadType = payloadType;
        Prefix = prefix;
        Fields = fields;
        Constructor = constructor;
    }

    public Type PayloadType { get; }

    public string Prefix { get; }

    public IReadOnlyList<CallbackDataField> Fields { get; }

    public ConstructorInfo? Constructor { get; }

    public static bool TryCreate(Type payloadType, out CallbackDataMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(payloadType);

        var attribute = payloadType.GetCustomAttribute<CallbackDataAttribute>(inherit: false);
        if (attribute is null)
        {
            metadata = null!;
            return false;
        }

        metadata = Create(payloadType, attribute.Prefix);
        return true;
    }

    private static CallbackDataMetadata Create(Type payloadType, string prefix)
    {
        ValidatePrefix(prefix, payloadType);

        var constructor = payloadType
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public)
            .Where(static candidate => candidate.GetParameters().Length > 0)
            .OrderByDescending(static candidate => candidate.GetParameters().Length)
            .FirstOrDefault();

        var fields = constructor is not null
            ? CreateConstructorFields(payloadType, constructor)
            : CreatePropertyFields(payloadType);

        return new CallbackDataMetadata(payloadType, prefix, fields, constructor);
    }

    private static CallbackDataField[] CreateConstructorFields(
        Type payloadType,
        ConstructorInfo constructor)
    {
        var properties = payloadType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.GetMethod is not null)
            .ToDictionary(static property => property.Name, StringComparer.OrdinalIgnoreCase);

        return constructor
            .GetParameters()
            .Select(parameter =>
            {
                if (!properties.TryGetValue(parameter.Name ?? string.Empty, out var property))
                {
                    throw new InvalidOperationException(
                        $"Callback data payload type '{payloadType.FullName}' constructor parameter '{parameter.Name}' does not map to a public property.");
                }

                ValidateFieldType(payloadType, property.PropertyType, property.Name);
                return new CallbackDataField(property, parameter);
            })
            .ToArray();
    }

    private static CallbackDataField[] CreatePropertyFields(Type payloadType)
    {
        var properties = payloadType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(static property => property.GetMethod is not null)
            .OrderBy(static property => property.MetadataToken)
            .ToArray();

        foreach (var property in properties)
        {
            if (property.SetMethod is null)
            {
                throw new InvalidOperationException(
                    $"Callback data payload type '{payloadType.FullName}' property '{property.Name}' must be settable when no public primary constructor is available.");
            }

            ValidateFieldType(payloadType, property.PropertyType, property.Name);
        }

        return properties
            .Select(static property => new CallbackDataField(property, Parameter: null))
            .ToArray();
    }

    private static void ValidatePrefix(string prefix, Type payloadType)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new InvalidOperationException(
                $"Callback data payload type '{payloadType.FullName}' must declare a non-empty callback data prefix.");
        }

        if (prefix.Contains(':', StringComparison.Ordinal) ||
            prefix.Contains('%', StringComparison.Ordinal) ||
            prefix.Any(char.IsWhiteSpace))
        {
            throw new InvalidOperationException(
                $"Callback data payload type '{payloadType.FullName}' prefix must not contain ':', '%', or whitespace.");
        }
    }

    private static void ValidateFieldType(Type payloadType, Type fieldType, string fieldName)
    {
        var type = Nullable.GetUnderlyingType(fieldType) ?? fieldType;

        if (Nullable.GetUnderlyingType(fieldType) is not null)
        {
            throw new InvalidOperationException(
                $"Callback data payload type '{payloadType.FullName}' field '{fieldName}' must not be nullable.");
        }

        if (type == typeof(string) ||
            type == typeof(int) ||
            type == typeof(long) ||
            type == typeof(bool) ||
            type.IsEnum)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Callback data payload type '{payloadType.FullName}' field '{fieldName}' has unsupported type '{fieldType.FullName}'. " +
            "Supported compact callback data types are string, int, long, bool, and enums.");
    }

    public string FormatField(object? value, Type fieldType)
    {
        if (value is null)
        {
            throw new InvalidOperationException(
                $"Callback data payload type '{PayloadType.FullName}' contains a null field value. Compact callback data fields must not be null.");
        }

        var type = Nullable.GetUnderlyingType(fieldType) ?? fieldType;

        string text = type == typeof(bool)
            ? ((bool)value ? "true" : "false")
            : type.IsEnum
                ? value.ToString()!
                : Convert.ToString(value, CultureInfo.InvariantCulture)!;

        return Escape(text);
    }

    public object ParseField(string value, Type fieldType)
    {
        var text = Unescape(value);
        var type = Nullable.GetUnderlyingType(fieldType) ?? fieldType;

        if (type == typeof(string))
        {
            return text;
        }

        if (type == typeof(int))
        {
            return int.Parse(text, CultureInfo.InvariantCulture);
        }

        if (type == typeof(long))
        {
            return long.Parse(text, CultureInfo.InvariantCulture);
        }

        if (type == typeof(bool))
        {
            return bool.Parse(text);
        }

        if (type.IsEnum)
        {
            return Enum.Parse(type, text, ignoreCase: false);
        }

        throw new InvalidOperationException(
            $"Callback data payload type '{PayloadType.FullName}' has unsupported field type '{fieldType.FullName}'.");
    }

    private static string Escape(string value)
    {
        return value
            .Replace("%", "%25", StringComparison.Ordinal)
            .Replace(":", "%3A", StringComparison.Ordinal);
    }

    private static string Unescape(string value)
    {
        return value
            .Replace("%3A", ":", StringComparison.Ordinal)
            .Replace("%25", "%", StringComparison.Ordinal);
    }
}

internal sealed record CallbackDataField(PropertyInfo Property, ParameterInfo? Parameter);
