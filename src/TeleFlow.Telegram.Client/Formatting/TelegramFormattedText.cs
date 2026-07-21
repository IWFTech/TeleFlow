namespace TeleFlow.Telegram.Formatting;

/// <summary>
/// Represents Telegram text rendered with an explicit Bot API parse mode.
/// Instances are created by the safe HTML and MarkdownV2 builders, then passed
/// to framework context helpers or unwrapped explicitly for generated Telegram client methods.
/// </summary>
public sealed class TelegramFormattedText
{
    internal TelegramFormattedText(string text, TelegramParseMode parseMode)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);

        Text = text;
        ParseMode = parseMode;
    }

    /// <summary>
    /// Gets the rendered text sent to Telegram.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets the explicit Bot API parse mode used to interpret <see cref="Text"/>.
    /// </summary>
    public TelegramParseMode ParseMode { get; }
}
