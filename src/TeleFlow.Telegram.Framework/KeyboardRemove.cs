using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram;

public sealed class KeyboardRemove
{
    private bool? _selective;

    private KeyboardRemove()
    {
    }

    public static KeyboardRemove Create()
    {
        return new KeyboardRemove();
    }

    public KeyboardRemove Selective(bool value = true)
    {
        _selective = value;
        return this;
    }

    public ReplyKeyboardRemove ToMarkup()
    {
        return new ReplyKeyboardRemove
        {
            RemoveKeyboard = true,
            Selective = _selective
        };
    }
}
