using TeleFlow.Telegram.Internal;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram;

public sealed class ForceReplyBuilder
{
    private string? _inputFieldPlaceholder;
    private bool? _selective;

    private ForceReplyBuilder()
    {
    }

    public static ForceReplyBuilder Create()
    {
        return new ForceReplyBuilder();
    }

    public ForceReplyBuilder Placeholder(string placeholder)
    {
        TelegramKeyboardPlaceholderValidator.ValidateForceReplyInputFieldPlaceholder(placeholder);

        _inputFieldPlaceholder = placeholder;
        return this;
    }

    public ForceReplyBuilder Selective(bool value = true)
    {
        _selective = value;
        return this;
    }

    public ForceReply ToMarkup()
    {
        return new ForceReply
        {
            ForceReplyValue = true,
            InputFieldPlaceholder = _inputFieldPlaceholder,
            Selective = _selective
        };
    }
}
