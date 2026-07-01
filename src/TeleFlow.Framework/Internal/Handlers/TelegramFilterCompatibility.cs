namespace TeleFlow.Telegram.Internal.Handlers;

internal static class TelegramFilterCompatibility
{
    public static bool Supports(
        TelegramFilterKind kind,
        TelegramHandlerKind handlerKind)
    {
        return GetTarget(kind) switch
        {
            TelegramFilterTarget.Chat => handlerKind is TelegramHandlerKind.Message or
                TelegramHandlerKind.Command or
                TelegramHandlerKind.Callback or
                TelegramHandlerKind.ChatMember,
            TelegramFilterTarget.MessageThread => handlerKind is TelegramHandlerKind.Message or
                TelegramHandlerKind.Command or
                TelegramHandlerKind.Callback,
            TelegramFilterTarget.Message => handlerKind is TelegramHandlerKind.Message or
                TelegramHandlerKind.Command,
            TelegramFilterTarget.Callback => handlerKind == TelegramHandlerKind.Callback,
            _ => false
        };
    }

    public static string GetInvalidPlacementMessage(
        TelegramFilterKind kind,
        TelegramHandlerKind handlerKind)
    {
        return TelegramFilterFacts.GetTarget(kind) switch
        {
            TelegramFilterTarget.Chat =>
                $"Chat filters cannot be used on {FormatHandlerKind(handlerKind)} handlers.",
            TelegramFilterTarget.MessageThread =>
                $"Message thread filters cannot be used on {FormatHandlerKind(handlerKind)} handlers.",
            TelegramFilterTarget.Message =>
                $"Message filters cannot be used on {FormatHandlerKind(handlerKind)} handlers.",
            TelegramFilterTarget.Callback =>
                $"Callback filters cannot be used on {FormatHandlerKind(handlerKind)} handlers.",
            _ => $"Telegram filter '{kind}' cannot be used on {FormatHandlerKind(handlerKind)} handlers."
        };
    }

    private static TelegramFilterTarget GetTarget(TelegramFilterKind kind)
    {
        return TelegramFilterFacts.GetTarget(kind);
    }

    private static string FormatHandlerKind(TelegramHandlerKind handlerKind)
    {
        return handlerKind switch
        {
            TelegramHandlerKind.Command => "command",
            TelegramHandlerKind.Message => "message",
            TelegramHandlerKind.Callback => "callback",
            TelegramHandlerKind.ChatMember => "chat member update",
            _ => handlerKind.ToString()
        };
    }
}
