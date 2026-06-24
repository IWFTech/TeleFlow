using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using TeleFlow.Telegram.Schema.Abstractions;

namespace TeleFlow.Telegram.Internal;

internal sealed class SchemaTelegramRequest<TResult> :
    ITelegramExecutableRequest<TelegramRequestResult<TResult>>
{
    private static readonly ConcurrentDictionary<Type, string> MethodNameCache = [];
    private readonly ITelegramApiMethod<TResult> _method;

    public SchemaTelegramRequest(ITelegramApiMethod<TResult> method)
    {
        ArgumentNullException.ThrowIfNull(method);
        _method = method;
    }

    public string MethodName => MethodNameCache.GetOrAdd(_method.GetType(), ResolveMethodName);

    public object Payload => _method;

    public TelegramRequestResult<TResult> DeserializeResponse(
        JsonSerializerOptions serializerOptions,
        string resultJson)
    {
        ArgumentNullException.ThrowIfNull(serializerOptions);
        ArgumentNullException.ThrowIfNull(resultJson);

        var result = JsonSerializer.Deserialize<TResult>(resultJson, serializerOptions);

        if (result is null)
        {
            throw new TelegramRequestException(
                $"Telegram response for method '{MethodName}' did not contain a deserializable result payload.");
        }

        return new TelegramRequestResult<TResult>(result);
    }

    private static string ResolveMethodName(Type methodType)
    {
        var property = methodType.GetProperty(
            "MethodName",
            BindingFlags.Public | BindingFlags.Static);

        if (property?.GetValue(null) is string methodName && !string.IsNullOrWhiteSpace(methodName))
        {
            return methodName;
        }

        throw new InvalidOperationException(
            $"Telegram schema method type '{methodType.FullName}' does not expose a public static MethodName property.");
    }
}
