using System.Text;
using TeleFlow.Core.Callbacks;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram;

/// <summary>
/// Builds Telegram inline keyboard markup for common button scenarios while keeping native
/// <see cref="InlineKeyboardMarkup"/> available for full Bot API control.
/// </summary>
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
        ValidateCallbackData(callbackData);

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
        return BuildCore(callbackData: null);
    }

    public InlineKeyboardMarkup Build(ICallbackDataSerializer callbackData)
    {
        ArgumentNullException.ThrowIfNull(callbackData);

        return BuildCore(callbackData);
    }

    private InlineKeyboardMarkup BuildCore(ICallbackDataSerializer? callbackData)
    {
        var rows = _rows
            .Where(static row => row.Count > 0)
            .Select(row => row
                .Select(intent => intent.ToButton(callbackData))
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

    private static void ValidateCallbackData(string callbackData)
    {
        if (string.IsNullOrWhiteSpace(callbackData))
        {
            throw new InvalidOperationException("Telegram callback data must not be empty.");
        }

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
        Func<ICallbackDataSerializer, string>? SerializePayload,
        Type? PayloadType,
        string? CallbackData,
        string? Url,
        InlineKeyboardButtonOptions? Options)
    {
        public static InlineKeyboardButtonIntent TypedPayload<TPayload>(
            string text,
            TPayload payload,
            InlineKeyboardButtonOptions? options)
        {
            return new InlineKeyboardButtonIntent(
                text,
                serializer => serializer.Serialize(payload),
                typeof(TPayload),
                CallbackData: null,
                Url: null,
                options);
        }

        public static InlineKeyboardButtonIntent RawCallbackData(
            string text,
            string callbackData,
            InlineKeyboardButtonOptions? options)
        {
            return new InlineKeyboardButtonIntent(
                text,
                SerializePayload: null,
                PayloadType: null,
                callbackData,
                Url: null,
                options);
        }

        public static InlineKeyboardButtonIntent Link(
            string text,
            string url,
            InlineKeyboardButtonOptions? options)
        {
            return new InlineKeyboardButtonIntent(
                text,
                SerializePayload: null,
                PayloadType: null,
                CallbackData: null,
                url,
                options);
        }

        public InlineKeyboardButton ToButton(ICallbackDataSerializer? callbackData)
        {
            if (SerializePayload is not null)
            {
                if (callbackData is null)
                {
                    throw new InvalidOperationException(
                        $"Inline keyboard typed callback payload '{PayloadType?.FullName}' requires ICallbackDataSerializer. " +
                        "Use Build(callbackData) or pass raw string callback data.");
                }

                var serializedPayload = SerializePayload(callbackData);
                ValidateCallbackData(serializedPayload);

                return ApplyOptions(new InlineKeyboardButton
                {
                    Text = Text,
                    CallbackData = serializedPayload
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
