using System.Reflection;

namespace TeleFlow.Telegram.Internal.Handlers;

internal delegate ValueTask<TelegramErrorHandlingResult> TelegramErrorHandlerGeneratedInvoker(
    IServiceProvider services,
    object?[] arguments,
    CancellationToken cancellationToken);

internal sealed class TelegramErrorHandlerDescriptor
{
    public TelegramErrorHandlerDescriptor(
        Type handlerType,
        MethodInfo method,
        Type? exceptionType,
        Type? telegramContextType,
        int registrationOrder,
        string? moduleName,
        IReadOnlyList<TelegramErrorHandlerParameterDescriptor> parameters)
        : this(
            handlerType,
            method,
            method.Name,
            generatedInvoker: null,
            exceptionType,
            telegramContextType,
            registrationOrder,
            moduleName,
            parameters)
    {
    }

    public TelegramErrorHandlerDescriptor(
        Type handlerType,
        string methodName,
        TelegramErrorHandlerGeneratedInvoker generatedInvoker,
        Type? exceptionType,
        Type? telegramContextType,
        int registrationOrder,
        string? moduleName,
        IReadOnlyList<TelegramErrorHandlerParameterDescriptor> parameters)
        : this(
            handlerType,
            method: null,
            methodName,
            generatedInvoker,
            exceptionType,
            telegramContextType,
            registrationOrder,
            moduleName,
            parameters)
    {
    }

    private TelegramErrorHandlerDescriptor(
        Type handlerType,
        MethodInfo? method,
        string methodName,
        TelegramErrorHandlerGeneratedInvoker? generatedInvoker,
        Type? exceptionType,
        Type? telegramContextType,
        int registrationOrder,
        string? moduleName,
        IReadOnlyList<TelegramErrorHandlerParameterDescriptor> parameters)
    {
        ArgumentNullException.ThrowIfNull(handlerType);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);
        ArgumentNullException.ThrowIfNull(parameters);

        HandlerType = handlerType;
        Method = method;
        MethodName = methodName;
        GeneratedInvoker = generatedInvoker;
        ExceptionType = exceptionType;
        TelegramContextType = telegramContextType;
        RegistrationOrder = registrationOrder;
        ModuleName = moduleName;
        Parameters = parameters;
    }

    public Type HandlerType { get; }

    public MethodInfo? Method { get; }

    public string MethodName { get; }

    public TelegramErrorHandlerGeneratedInvoker? GeneratedInvoker { get; }

    public Type? ExceptionType { get; }

    public Type? TelegramContextType { get; }

    public int RegistrationOrder { get; }

    public string? ModuleName { get; }

    public IReadOnlyList<TelegramErrorHandlerParameterDescriptor> Parameters { get; }
}
