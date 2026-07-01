using TeleFlow.Telegram.Internal;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram;

public sealed class ReplyKeyboard
{
    private readonly List<List<KeyboardButton>> _rows = [[]];
    private bool? _isPersistent;
    private bool? _resizeKeyboard;
    private bool? _oneTimeKeyboard;
    private string? _inputFieldPlaceholder;
    private bool? _selective;

    private ReplyKeyboard()
    {
    }

    public static ReplyKeyboard Create()
    {
        return new ReplyKeyboard();
    }

    public ReplyKeyboard Button(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        CurrentRow.Add(new KeyboardButton { Text = text });
        return this;
    }

    public ReplyKeyboard Button(KeyboardButton button)
    {
        ArgumentNullException.ThrowIfNull(button);

        if (string.IsNullOrWhiteSpace(button.Text))
        {
            throw new ArgumentException("Reply keyboard button text must not be empty.", nameof(button));
        }

        CurrentRow.Add(button);
        return this;
    }

    public ReplyKeyboard Row()
    {
        if (CurrentRow.Count > 0)
        {
            _rows.Add([]);
        }

        return this;
    }

    public ReplyKeyboard Persistent(bool value = true)
    {
        _isPersistent = value;
        return this;
    }

    public ReplyKeyboard Resize(bool value = true)
    {
        _resizeKeyboard = value;
        return this;
    }

    public ReplyKeyboard OneTime(bool value = true)
    {
        _oneTimeKeyboard = value;
        return this;
    }

    public ReplyKeyboard Placeholder(string placeholder)
    {
        TelegramKeyboardPlaceholderValidator.ValidateReplyKeyboardInputFieldPlaceholder(placeholder);

        _inputFieldPlaceholder = placeholder;
        return this;
    }

    public ReplyKeyboard Selective(bool value = true)
    {
        _selective = value;
        return this;
    }

    public ReplyKeyboardMarkup ToMarkup()
    {
        var rows = _rows
            .Where(static row => row.Count > 0)
            .Select(static row => row.ToArray())
            .ToArray();

        if (rows.Length == 0)
        {
            throw new InvalidOperationException("Reply keyboard must contain at least one button.");
        }

        return new ReplyKeyboardMarkup
        {
            Keyboard = rows,
            IsPersistent = _isPersistent,
            ResizeKeyboard = _resizeKeyboard,
            OneTimeKeyboard = _oneTimeKeyboard,
            InputFieldPlaceholder = _inputFieldPlaceholder,
            Selective = _selective
        };
    }

    private List<KeyboardButton> CurrentRow => _rows[^1];
}
