using System.ComponentModel;

namespace TeleFlow.Telegram;

/// <summary>
/// Infrastructure error handler metadata emitted by TeleFlow source generators.
/// This API is not intended to be used by application code.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TelegramGeneratedErrorHandlerDescriptor
{
    public TelegramGeneratedErrorHandlerDescriptor(
        Type handlerType,
        string methodName,
        Type? exceptionType,
        Type? telegramContextType,
        int registrationOrder,
        string? moduleName,
        IReadOnlyList<TelegramGeneratedErrorHandlerParameterDescriptor> parameters,
        TelegramGeneratedErrorHandlerInvoker invoker)
    {
        ArgumentNullException.ThrowIfNull(handlerType);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(invoker);

        if (exceptionType is not null && !typeof(Exception).IsAssignableFrom(exceptionType))
        {
            throw new InvalidOperationException(
                $"Generated Telegram error handler exception type '{exceptionType.FullName}' must derive from Exception.");
        }

        if (telegramContextType is not null && !typeof(TelegramUpdateContext).IsAssignableFrom(telegramContextType))
        {
            throw new InvalidOperationException(
                $"Generated Telegram error handler context type '{telegramContextType.FullName}' must derive from TelegramUpdateContext.");
        }

        HandlerType = handlerType;
        MethodName = methodName;
        ExceptionType = exceptionType;
        TelegramContextType = telegramContextType;
        RegistrationOrder = registrationOrder;
        ModuleName = moduleName;
        Parameters = CopyValues(parameters);
        Invoker = invoker;
    }

    public Type HandlerType { get; }

    public string MethodName { get; }

    public Type? ExceptionType { get; }

    public Type? TelegramContextType { get; }

    public int RegistrationOrder { get; }

    public string? ModuleName { get; }

    public IReadOnlyList<TelegramGeneratedErrorHandlerParameterDescriptor> Parameters { get; }

    public TelegramGeneratedErrorHandlerInvoker Invoker { get; }

    private static TValue[] CopyValues<TValue>(IReadOnlyList<TValue>? values)
    {
        return values is null || values.Count == 0
            ? []
            : values.ToArray();
    }
}
