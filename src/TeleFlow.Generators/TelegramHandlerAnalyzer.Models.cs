using Microsoft.CodeAnalysis;

namespace TeleFlow.Generators;

public sealed partial class TelegramHandlerAnalyzer
{
    private enum HandlerKind
    {
        Command,
        Message,
        Callback,
        ChatMember
    }

    private sealed record CommandRegistration(string Key, string Display, Location? Location);

    private sealed record CallbackPrefixRegistration(string Prefix, Location? Location);
}
