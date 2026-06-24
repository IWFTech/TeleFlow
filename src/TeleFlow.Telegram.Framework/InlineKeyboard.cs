using System.Text;
using TeleFlow.Core.Callbacks;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram;

public sealed class InlineKeyboard
{
    private const int MaxTelegramCallbackDataBytes = 64;

    private readonly List<List<InlineKeyboardButtonIntent>> _rows = [[]];

    private InlineKeyboard()
    {
    }

    public static InlineKeyboard Create()
    {
        return new InlineKeyboard();
    }

    public InlineKeyboard Button<TPayload>(string text, TPayload payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(payload);

        CurrentRow.Add(new InlineKeyboardButtonIntent(text, Payload: payload, CallbackData: null, Url: null));
        return this;
    }

    public InlineKeyboard Button(string text, string callbackData)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(callbackData);
        ValidateCallbackDataLength(callbackData);

        CurrentRow.Add(new InlineKeyboardButtonIntent(text, Payload: null, callbackData, Url: null));
        return this;
    }

    public InlineKeyboard Url(string text, string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            throw new ArgumentException("Inline keyboard URL button must use an absolute URL.", nameof(url));
        }

        return UrlCore(text, url);
    }

    public InlineKeyboard Url(string text, Uri url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(url);

        if (!url.IsAbsoluteUri)
        {
            throw new ArgumentException("Inline keyboard URL button must use an absolute URL.", nameof(url));
        }

        return UrlCore(text, url.OriginalString);
    }

    private InlineKeyboard UrlCore(string text, string url)
    {
        CurrentRow.Add(new InlineKeyboardButtonIntent(text, Payload: null, CallbackData: null, url));
        return this;
    }

    public InlineKeyboard Row()
    {
        if (CurrentRow.Count > 0)
        {
            _rows.Add([]);
        }

        return this;
    }

    public InlineKeyboardMarkup ToMarkup(ICallbackDataSerializer callbackDataSerializer)
    {
        ArgumentNullException.ThrowIfNull(callbackDataSerializer);

        var rows = _rows
            .Where(static row => row.Count > 0)
            .Select(row => row
                .Select(intent => intent.ToButton(callbackDataSerializer))
                .ToArray())
            .ToArray();

        if (rows.Length == 0)
        {
            throw new InvalidOperationException("Inline keyboard must contain at least one button.");
        }

        return new InlineKeyboardMarkup
        {
            InlineKeyboard = rows
        };
    }

    private List<InlineKeyboardButtonIntent> CurrentRow => _rows[^1];

    private static void ValidateCallbackDataLength(string callbackData)
    {
        if (Encoding.UTF8.GetByteCount(callbackData) > MaxTelegramCallbackDataBytes)
        {
            throw new InvalidOperationException(
                $"Telegram callback data must be at most {MaxTelegramCallbackDataBytes} UTF-8 bytes.");
        }
    }

    private sealed record InlineKeyboardButtonIntent(
        string Text,
        object? Payload,
        string? CallbackData,
        string? Url)
    {
        public InlineKeyboardButton ToButton(ICallbackDataSerializer callbackDataSerializer)
        {
            if (Payload is not null)
            {
                var serializeMethod = typeof(ICallbackDataSerializer)
                    .GetMethod(nameof(ICallbackDataSerializer.Serialize))!
                    .MakeGenericMethod(Payload.GetType());
                var callbackData = (string)serializeMethod.Invoke(callbackDataSerializer, [Payload])!;

                return new InlineKeyboardButton
                {
                    Text = Text,
                    CallbackData = callbackData
                };
            }

            if (CallbackData is not null)
            {
                return new InlineKeyboardButton
                {
                    Text = Text,
                    CallbackData = CallbackData
                };
            }

            if (Url is not null)
            {
                return new InlineKeyboardButton
                {
                    Text = Text,
                    Url = Url
                };
            }

            throw new InvalidOperationException("Inline keyboard button intent does not contain an action.");
        }
    }
}
