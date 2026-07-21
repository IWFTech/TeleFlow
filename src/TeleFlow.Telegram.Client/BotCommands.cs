using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram;

/// <summary>
/// Creates explicit Bot API command descriptors for application startup configuration.
/// It does not discover handler routes or publish commands automatically.
/// </summary>
public static class BotCommands
{
    public static BotCommand Create(string command, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new BotCommand
        {
            Command = command,
            Description = description
        };
    }

    public static BotCommand Ephemeral(string command, string description)
    {
        return Create(command, description) with
        {
            IsEphemeral = true
        };
    }
}
