using System.Text;

namespace TeleFlow.Telegram.Formatting;

/// <summary>
/// Composes safe formatted text for one explicit Telegram Bot API parse mode.
/// The builder escapes plain values and keeps rendered fragments separate from
/// application strings, preventing accidental double escaping during composition.
/// </summary>
public sealed class TelegramTextBuilder
{
    private readonly TelegramTextRenderer _renderer;
    private readonly StringBuilder _content = new();

    internal TelegramTextBuilder(TelegramTextRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        _renderer = renderer;
    }

    /// <summary>
    /// Appends plain text after escaping it for the selected parse mode.
    /// </summary>
    public TelegramTextBuilder Text(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _content.Append(_renderer.EscapeText(text));
        return this;
    }

    /// <summary>
    /// Appends a line break.
    /// </summary>
    public TelegramTextBuilder LineBreak()
    {
        _content.Append('\n');
        return this;
    }

    /// <summary>
    /// Appends a formatted value that uses the same parse mode as this builder.
    /// </summary>
    public TelegramTextBuilder Append(TelegramFormattedText text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.ParseMode != _renderer.ParseMode)
        {
            throw new InvalidOperationException(
                $"Formatted text with parse mode '{text.ParseMode}' cannot be appended to a builder using '{_renderer.ParseMode}'.");
        }

        _content.Append(text.Text);
        return this;
    }

    /// <summary>
    /// Appends bold text.
    /// </summary>
    public TelegramTextBuilder Bold(string text)
    {
        return AppendWrapped(text, _renderer.Bold);
    }

    /// <summary>
    /// Appends bold nested content.
    /// </summary>
    public TelegramTextBuilder Bold(Action<TelegramTextBuilder> content)
    {
        return AppendNested(content, _renderer.Bold);
    }

    /// <summary>
    /// Appends italic text.
    /// </summary>
    public TelegramTextBuilder Italic(string text)
    {
        return AppendWrapped(text, _renderer.Italic);
    }

    /// <summary>
    /// Appends italic nested content.
    /// </summary>
    public TelegramTextBuilder Italic(Action<TelegramTextBuilder> content)
    {
        return AppendNested(content, _renderer.Italic);
    }

    /// <summary>
    /// Appends underlined text.
    /// </summary>
    public TelegramTextBuilder Underline(string text)
    {
        return AppendWrapped(text, _renderer.Underline);
    }

    /// <summary>
    /// Appends underlined nested content.
    /// </summary>
    public TelegramTextBuilder Underline(Action<TelegramTextBuilder> content)
    {
        return AppendNested(content, _renderer.Underline);
    }

    /// <summary>
    /// Appends strike-through text.
    /// </summary>
    public TelegramTextBuilder Strikethrough(string text)
    {
        return AppendWrapped(text, _renderer.Strikethrough);
    }

    /// <summary>
    /// Appends strike-through nested content.
    /// </summary>
    public TelegramTextBuilder Strikethrough(Action<TelegramTextBuilder> content)
    {
        return AppendNested(content, _renderer.Strikethrough);
    }

    /// <summary>
    /// Appends spoiler text.
    /// </summary>
    public TelegramTextBuilder Spoiler(string text)
    {
        return AppendWrapped(text, _renderer.Spoiler);
    }

    /// <summary>
    /// Appends spoiler nested content.
    /// </summary>
    public TelegramTextBuilder Spoiler(Action<TelegramTextBuilder> content)
    {
        return AppendNested(content, _renderer.Spoiler);
    }

    /// <summary>
    /// Appends inline code. Nested Telegram entities are intentionally not supported inside code.
    /// </summary>
    public TelegramTextBuilder Code(string text)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        _content.Append(_renderer.Code(text));
        return this;
    }

    /// <summary>
    /// Appends a preformatted block. Nested Telegram entities are intentionally not supported inside the block.
    /// </summary>
    public TelegramTextBuilder Pre(string text, string? language = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);

        if (language is { Length: 0 })
        {
            throw new ArgumentException("Preformatted language must be null or non-empty.", nameof(language));
        }

        _content.Append(_renderer.Pre(text, language));
        return this;
    }

    /// <summary>
    /// Appends a link with an escaped label and target.
    /// </summary>
    public TelegramTextBuilder Link(Uri uri, string label)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentException.ThrowIfNullOrEmpty(label);

        if (!uri.IsAbsoluteUri)
        {
            throw new ArgumentException("Telegram links must use an absolute URI.", nameof(uri));
        }

        _content.Append(_renderer.Link(uri.OriginalString, label));
        return this;
    }

    /// <summary>
    /// Appends a Telegram user mention with an escaped label.
    /// </summary>
    public TelegramTextBuilder Mention(long userId, string label)
    {
        if (userId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(userId), userId, "Telegram user id must be positive.");
        }

        ArgumentException.ThrowIfNullOrEmpty(label);
        _content.Append(_renderer.Mention(userId, label));
        return this;
    }

    /// <summary>
    /// Appends a block quote. Nested entities are intentionally not supported inside the quote.
    /// </summary>
    public TelegramTextBuilder BlockQuote(string text, bool expandable = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        _content.Append(_renderer.BlockQuote(text, expandable));
        return this;
    }

    /// <summary>
    /// Appends a custom emoji with a required fallback emoji.
    /// Telegram may show the fallback in notifications, forwards, and unsupported clients.
    /// </summary>
    public TelegramTextBuilder CustomEmoji(string customEmojiId, string fallbackEmoji)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customEmojiId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fallbackEmoji);
        _content.Append(_renderer.CustomEmoji(customEmojiId, fallbackEmoji));
        return this;
    }

    /// <summary>
    /// Builds an immutable value for framework helpers or explicit generated-client arguments.
    /// </summary>
    public TelegramFormattedText Build()
    {
        var text = _content.ToString();

        if (text.Length == 0)
        {
            throw new InvalidOperationException("Formatted text must not be empty.");
        }

        return new TelegramFormattedText(text, _renderer.ParseMode);
    }

    private TelegramTextBuilder AppendWrapped(string text, Func<string, string> format)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        _content.Append(format(_renderer.EscapeText(text)));
        return this;
    }

    private TelegramTextBuilder AppendNested(
        Action<TelegramTextBuilder> content,
        Func<string, string> format)
    {
        ArgumentNullException.ThrowIfNull(content);

        var nested = new TelegramTextBuilder(_renderer);
        content(nested);
        var nestedText = nested._content.ToString();

        if (nestedText.Length == 0)
        {
            throw new InvalidOperationException("Formatted nested content must not be empty.");
        }

        _content.Append(format(nestedText));
        return this;
    }
}

/// <summary>
/// Renders the shared safe formatting vocabulary for one Telegram parse mode.
/// It is intentionally local to the client package and has no framework or transport dependency.
/// </summary>
internal abstract class TelegramTextRenderer
{
    public abstract TelegramParseMode ParseMode { get; }

    public abstract string EscapeText(string text);

    public abstract string Bold(string text);

    public abstract string Italic(string text);

    public abstract string Underline(string text);

    public abstract string Strikethrough(string text);

    public abstract string Spoiler(string text);

    public abstract string Code(string text);

    public abstract string Pre(string text, string? language);

    public abstract string Link(string uri, string label);

    public abstract string Mention(long userId, string label);

    public abstract string BlockQuote(string text, bool expandable);

    public abstract string CustomEmoji(string customEmojiId, string fallbackEmoji);
}

/// <summary>
/// Renders safe Telegram HTML fragments for normal Bot API text messages.
/// </summary>
internal sealed class TelegramHtmlTextRenderer : TelegramTextRenderer
{
    public static TelegramHtmlTextRenderer Instance { get; } = new();

    public override TelegramParseMode ParseMode => TelegramParseMode.Html;

    public override string EscapeText(string text)
    {
        return TelegramHtmlEscaper.EscapeText(text);
    }

    public override string Bold(string text) => $"<b>{text}</b>";

    public override string Italic(string text) => $"<i>{text}</i>";

    public override string Underline(string text) => $"<u>{text}</u>";

    public override string Strikethrough(string text) => $"<s>{text}</s>";

    public override string Spoiler(string text) => $"<tg-spoiler>{text}</tg-spoiler>";

    public override string Code(string text) => $"<code>{EscapeText(text)}</code>";

    public override string Pre(string text, string? language)
    {
        var escapedText = EscapeText(text);

        if (language is null)
        {
            return $"<pre>{escapedText}</pre>";
        }

        ValidateLanguage(language);
        return $"<pre><code class=\"language-{EscapeAttribute(language)}\">{escapedText}</code></pre>";
    }

    public override string Link(string uri, string label)
    {
        return $"<a href=\"{EscapeAttribute(uri)}\">{EscapeText(label)}</a>";
    }

    public override string Mention(long userId, string label)
    {
        return Link($"tg://user?id={userId}", label);
    }

    public override string BlockQuote(string text, bool expandable)
    {
        return expandable
            ? $"<blockquote expandable>{EscapeText(text)}</blockquote>"
            : $"<blockquote>{EscapeText(text)}</blockquote>";
    }

    public override string CustomEmoji(string customEmojiId, string fallbackEmoji)
    {
        return $"<tg-emoji emoji-id=\"{EscapeAttribute(customEmojiId)}\">{EscapeText(fallbackEmoji)}</tg-emoji>";
    }

    private static string EscapeAttribute(string value)
    {
        return TelegramHtmlEscaper.EscapeInterpolation(value);
    }

    private static void ValidateLanguage(string language)
    {
        if (language.Any(static character =>
                !char.IsAsciiLetterOrDigit(character) &&
                character is not '-' and not '_'))
        {
            throw new ArgumentException(
                "Preformatted language may contain only ASCII letters, digits, '-' and '_'.",
                nameof(language));
        }
    }
}

/// <summary>
/// Renders safe Telegram MarkdownV2 fragments for normal Bot API text messages.
/// </summary>
internal sealed class TelegramMarkdownV2TextRenderer : TelegramTextRenderer
{
    private const string ReservedCharacters = "_*[]()~`>#+-=|{}.!\\";

    public static TelegramMarkdownV2TextRenderer Instance { get; } = new();

    public override TelegramParseMode ParseMode => TelegramParseMode.MarkdownV2;

    public override string EscapeText(string text)
    {
        var builder = new StringBuilder(text.Length);

        foreach (var character in text)
        {
            if (ReservedCharacters.Contains(character))
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    public override string Bold(string text) => $"*{text}*";

    public override string Italic(string text) => $"_{text}_";

    public override string Underline(string text) => $"__{text}__";

    public override string Strikethrough(string text) => $"~{text}~";

    public override string Spoiler(string text) => $"||{text}||";

    public override string Code(string text) => $"`{EscapeCode(text)}`";

    public override string Pre(string text, string? language)
    {
        if (language is not null)
        {
            ValidateLanguage(language);
        }

        return language is null
            ? $"```\n{EscapeCode(text)}\n```"
            : $"```{language}\n{EscapeCode(text)}\n```";
    }

    public override string Link(string uri, string label)
    {
        return $"[{EscapeText(label)}]({EscapeLinkTarget(uri)})";
    }

    public override string Mention(long userId, string label)
    {
        return Link($"tg://user?id={userId}", label);
    }

    public override string BlockQuote(string text, bool expandable)
    {
        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
        var builder = new StringBuilder(text.Length + lines.Length * 2);

        for (var index = 0; index < lines.Length; index++)
        {
            builder.Append("> ");
            builder.Append(EscapeText(lines[index]));

            if (index == lines.Length - 1 && expandable)
            {
                builder.Append("||");
            }

            if (index < lines.Length - 1)
            {
                builder.Append('\n');
            }
        }

        return builder.ToString();
    }

    public override string CustomEmoji(string customEmojiId, string fallbackEmoji)
    {
        return $"![{EscapeText(fallbackEmoji)}](tg://emoji?id={EscapeLinkTarget(customEmojiId)})";
    }

    private static string EscapeCode(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal);
    }

    private static string EscapeLinkTarget(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }

    private static void ValidateLanguage(string language)
    {
        if (language.Any(static character =>
                !char.IsAsciiLetterOrDigit(character) &&
                character is not '-' and not '_'))
        {
            throw new ArgumentException(
                "Preformatted language may contain only ASCII letters, digits, '-' and '_'.",
                nameof(language));
        }
    }
}
