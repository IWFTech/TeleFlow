using System.Reflection;

namespace TeleFlow.Telegram.Internal.Handlers;

internal enum TelegramHandlerRegistrationMode
{
    Direct,
    Generated,
    Reflection
}

internal sealed class TelegramHandlerAssemblyRegistrationMarker
{
    public TelegramHandlerAssemblyRegistrationMarker(
        Assembly assembly,
        TelegramHandlerRegistrationMode mode)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        Assembly = assembly;
        Mode = mode;
    }

    public Assembly Assembly { get; }

    public TelegramHandlerRegistrationMode Mode { get; }
}

internal sealed class TelegramHandlerTypeRegistrationMarker
{
    public TelegramHandlerTypeRegistrationMarker(
        Type handlerType,
        TelegramHandlerRegistrationMode mode)
    {
        ArgumentNullException.ThrowIfNull(handlerType);

        HandlerType = handlerType;
        Mode = mode;
    }

    public Type HandlerType { get; }

    public TelegramHandlerRegistrationMode Mode { get; }
}
