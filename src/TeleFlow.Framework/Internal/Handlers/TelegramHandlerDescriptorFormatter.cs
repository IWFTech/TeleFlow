namespace TeleFlow.Telegram.Internal.Handlers;

internal static class TelegramHandlerDescriptorFormatter
{
    public static string GetDisplayName(TelegramHandlerDescriptor handler)
    {
        return $"{handler.HandlerType.FullName}.{handler.MethodName}";
    }
}
