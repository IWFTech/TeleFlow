using System.Collections;
using System.Collections.Concurrent;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Internal;

/// <summary>
/// Detects whether a Telegram request payload can contain files that must be sent as multipart content.
/// This protects the common JSON-only path from unnecessary recursive object scans.
/// It is used before sending outgoing Bot API requests such as sendMessage, sendPhoto, and media groups.
/// </summary>
internal static class TelegramRequestUploadDetector
{
    private static readonly ConcurrentDictionary<Type, bool> CapabilityCache = new();

    // Type-level proof used before touching object values.
    // false means "cannot contain InputFile"; true means "maybe, inspect the actual value".
    public static bool MayContainInputFile(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return CapabilityCache.GetOrAdd(
            type,
            static candidate => MayContainInputFile(candidate, []));
    }

    public static bool ContainsInputFile(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return ContainsInputFile(value, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    // Top-level method properties can be InputFile directly, or a generated union wrapper whose
    // selected case is InputFile. Both are sent as normal multipart file fields.
    public static bool TryGetDirectInputFile(object value, out InputFile inputFile)
    {
        ArgumentNullException.ThrowIfNull(value);

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

    // Generated Telegram union wrappers have no [JsonPropertyName] fields of their own.
    // Exactly one non-null public property is treated as the active union case.
    public static TelegramActiveUnionCase? GetActiveUnionCase(object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var metadata = TelegramRequestTypeMetadataCache.Get(value.GetType());
        if (metadata.TelegramProperties.Length > 0)
        {
            return null;
        }

        var activeCases = metadata.ReadableProperties
            .Select(property => new TelegramActiveUnionCase(property.Name, property.GetValue(value)))
            .Where(static item => item.Value is not null)
            .ToArray();

        return activeCases.Length == 1 ? activeCases[0] : null;
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
        if (!MayContainInputFile(type))
        {
            return false;
        }

        var metadata = TelegramRequestTypeMetadataCache.Get(type);
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

    private static bool MayContainInputFile(Type type, HashSet<Type> visiting)
    {
        var candidate = Nullable.GetUnderlyingType(type) ?? type;

        if (candidate == typeof(InputFile))
        {
            return true;
        }

        if (TelegramRequestTypeMetadataCache.IsScalarType(candidate) ||
            typeof(Stream).IsAssignableFrom(candidate))
        {
            return false;
        }

        if (candidate == typeof(object) || candidate.IsGenericParameter)
        {
            return true;
        }

        if (TryGetEnumerableElementType(candidate, out var elementType))
        {
            return elementType is null || MayContainInputFile(elementType, visiting);
        }

        // Interfaces, abstract classes, and replaceable classes are conservative by design:
        // the declared type may be safe, but a runtime implementation/subclass may contain InputFile.
        if (candidate.IsInterface || candidate.IsAbstract || !candidate.IsSealed)
        {
            return true;
        }

        // This guard is for recursive type graphs. Runtime object cycles are handled by
        // ContainsInputFile with reference equality tracking.
        if (!visiting.Add(candidate))
        {
            return false;
        }

        try
        {
            foreach (var property in TelegramRequestTypeMetadataCache.Get(candidate).ReadableProperties)
            {
                if (MayContainInputFile(property.PropertyType, visiting))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            visiting.Remove(candidate);
        }
    }

    private static bool TryGetEnumerableElementType(Type type, out Type? elementType)
    {
        if (type == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(type))
        {
            elementType = null;
            return false;
        }

        if (type.IsArray)
        {
            elementType = type.GetElementType();
            return true;
        }

        var enumerableType = type
            .GetInterfaces()
            .Append(type)
            .FirstOrDefault(static candidate =>
                candidate.IsGenericType &&
                candidate.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        elementType = enumerableType?.GetGenericArguments()[0];
        return true;
    }
}

/// <summary>
/// Represents the selected case of a generated Telegram union wrapper.
/// Request content builders use it to serialize unions as the active value expected by Telegram.
/// </summary>
internal sealed record TelegramActiveUnionCase(string Name, object? Value);
