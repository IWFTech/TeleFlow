using System.Text;
using TeleFlow.Telegram.Internal;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram;

public sealed class InlineKeyboardBuilder
{
    private const int MaxTelegramCallbackDataBytes = 64;

    private readonly List<List<InlineKeyboardButtonIntent>> _rows = [[]];

    private InlineKeyboardBuilder()
    {
    }

    public static InlineKeyboardBuilder Create()
    {
        return new InlineKeyboardBuilder();
    }

    public InlineKeyboardBuilder Button<TPayload>(
        string text,
        TPayload payload,
        InlineKeyboardButtonOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(payload);

        ValidateOptions(options);
        CurrentRow.Add(InlineKeyboardButtonIntent.TypedPayload(text, payload, options));
        return this;
    }

    public InlineKeyboardBuilder Button(
        string text,
        string callbackData,
        InlineKeyboardButtonOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(callbackData);
        ValidateCallbackDataLength(callbackData);

        ValidateOptions(options);
        CurrentRow.Add(InlineKeyboardButtonIntent.RawCallbackData(text, callbackData, options));
        return this;
    }

    public InlineKeyboardBuilder Url(
        string text,
        string url,
        InlineKeyboardButtonOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        if (!Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            throw new ArgumentException("Inline keyboard URL button must use an absolute URL.", nameof(url));
        }

        ValidateOptions(options);
        return UrlCore(text, url, options);
    }

    public InlineKeyboardBuilder Url(
        string text,
        Uri url,
        InlineKeyboardButtonOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentNullException.ThrowIfNull(url);

        if (!url.IsAbsoluteUri)
        {
            throw new ArgumentException("Inline keyboard URL button must use an absolute URL.", nameof(url));
        }

        ValidateOptions(options);
        return UrlCore(text, url.OriginalString, options);
    }

    public InlineKeyboardBuilder Row()
    {
        if (CurrentRow.Count > 0)
        {
            _rows.Add([]);
        }

        return this;
    }

    public InlineKeyboardMarkup Build()
    {
        var rows = _rows
            .Where(static row => row.Count > 0)
            .Select(static row => row
                .Select(static intent => intent.ToButton())
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

    private InlineKeyboardBuilder UrlCore(
        string text,
        string url,
        InlineKeyboardButtonOptions? options)
    {
        CurrentRow.Add(InlineKeyboardButtonIntent.Link(text, url, options));
        return this;
    }

    private static string PackTypedCallbackPayload(object payload)
    {
        var payloadType = payload.GetType();

        if (!CallbackDataMetadata.TryCreate(payloadType, out var metadata))
        {
            throw new InvalidOperationException(
                $"Inline keyboard callback payload type '{payloadType.FullName}' must declare CallbackDataAttribute. " +
                "Pass raw string callback data when using a custom callback data format.");
        }

        var callbackData = metadata.Pack(payload);
        ValidateCallbackDataLength(callbackData);
        return callbackData;
    }

    private static void ValidateCallbackDataLength(string callbackData)
    {
        if (Encoding.UTF8.GetByteCount(callbackData) > MaxTelegramCallbackDataBytes)
        {
            throw new InvalidOperationException(
                $"Telegram callback data must be at most {MaxTelegramCallbackDataBytes} UTF-8 bytes.");
        }
    }

    private static void ValidateOptions(InlineKeyboardButtonOptions? options)
    {
        if (options is null)
        {
            return;
        }

        if (options.Style is { Length: 0 })
        {
            throw new ArgumentException("Inline keyboard button style cannot be empty.", nameof(options));
        }

        if (options.IconCustomEmojiId is { Length: 0 })
        {
            throw new ArgumentException("Inline keyboard button custom emoji icon id cannot be empty.", nameof(options));
        }
    }

    private sealed record InlineKeyboardButtonIntent(
        string Text,
        object? Payload,
        string? CallbackData,
        string? Url,
        InlineKeyboardButtonOptions? Options)
    {
        public static InlineKeyboardButtonIntent TypedPayload(
            string text,
            object payload,
            InlineKeyboardButtonOptions? options)
        {
            return new InlineKeyboardButtonIntent(text, payload, CallbackData: null, Url: null, options);
        }

        public static InlineKeyboardButtonIntent RawCallbackData(
            string text,
            string callbackData,
            InlineKeyboardButtonOptions? options)
        {
            return new InlineKeyboardButtonIntent(text, Payload: null, callbackData, Url: null, options);
        }

        public static InlineKeyboardButtonIntent Link(
            string text,
            string url,
            InlineKeyboardButtonOptions? options)
        {
            return new InlineKeyboardButtonIntent(text, Payload: null, CallbackData: null, url, options);
        }

        public InlineKeyboardButton ToButton()
        {
            if (Payload is not null)
            {
                return ApplyOptions(new InlineKeyboardButton
                {
                    Text = Text,
                    CallbackData = PackTypedCallbackPayload(Payload)
                });
            }

            if (CallbackData is not null)
            {
                return ApplyOptions(new InlineKeyboardButton
                {
                    Text = Text,
                    CallbackData = CallbackData
                });
            }

            if (Url is not null)
            {
                return ApplyOptions(new InlineKeyboardButton
                {
                    Text = Text,
                    Url = Url
                });
            }

            throw new InvalidOperationException("Inline keyboard button intent does not contain an action.");
        }

        private InlineKeyboardButton ApplyOptions(InlineKeyboardButton button)
        {
            return button with
            {
                IconCustomEmojiId = Options?.IconCustomEmojiId,
                Style = Options?.Style
            };
        }
    }
}
