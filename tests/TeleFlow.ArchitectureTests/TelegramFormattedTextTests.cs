using TeleFlow.Telegram;
using TeleFlow.Telegram.Formatting;

namespace TeleFlow.ArchitectureTests;

public sealed class TelegramFormattedTextTests
{
    [Fact]
    public void TelegramHtmlBuilder_EscapesPlainTextAndAttributes()
    {
        var text = TelegramHtml.Create()
            .Text("A < & >")
            .Bold("x < y")
            .Link(new Uri("https://example.com/?a=1&b=2"), "A & B")
            .CustomEmoji("123", "💎")
            .Build();

        Assert.Equal(TelegramParseMode.Html, text.ParseMode);
        Assert.Equal(
            "A &lt; &amp; &gt;<b>x &lt; y</b><a href=\"https://example.com/?a=1&amp;b=2\">A &amp; B</a><tg-emoji emoji-id=\"123\">💎</tg-emoji>",
            text.Text);
    }

    [Fact]
    public void TelegramHtmlBuilder_ComposesNestedContentWithoutDoubleEscaping()
    {
        var text = TelegramHtml.Create()
            .Bold(content => content
                .Text("Active ")
                .Spoiler("secret & more"))
            .Build();

        Assert.Equal("<b>Active <tg-spoiler>secret &amp; more</tg-spoiler></b>", text.Text);
    }

    [Fact]
    public void TelegramMarkdownV2Builder_EscapesReservedTextCharacters()
    {
        var text = TelegramMarkdownV2.Create()
            .Text("_*[]()~`>#+-=|{}.!\\")
            .Bold("A * B")
            .Text(" ")
            .Link(new Uri("https://example.com/a_(b)"), "Open [profile]")
            .Text(" ")
            .CustomEmoji("123", "💎")
            .Build();

        Assert.Equal(TelegramParseMode.MarkdownV2, text.ParseMode);
        Assert.Equal(
            "\\_\\*\\[\\]\\(\\)\\~\\`\\>\\#\\+\\-\\=\\|\\{\\}\\.\\!\\\\*A \\* B* [Open \\[profile\\]](https://example.com/a_(b\\)) ![💎](tg://emoji?id=123)",
            text.Text);
    }

    [Fact]
    public void TelegramMarkdownV2Builder_FormatsCodeAndPreformattedText()
    {
        var text = TelegramMarkdownV2.Create()
            .Code("a`b\\c")
            .LineBreak()
            .Pre("x`y\\z", language: "csharp")
            .Build();

        Assert.Equal("`a\\`b\\\\c`\n```csharp\nx\\`y\\\\z\n```", text.Text);
    }

    [Fact]
    public void TelegramFormattedTextBuilder_FormatsBlockQuotesAndMentions()
    {
        var html = TelegramHtml.Create()
            .BlockQuote("First\nSecond", expandable: true)
            .Mention(42, "User & Co")
            .Build();
        var markdown = TelegramMarkdownV2.Create()
            .BlockQuote("First\nSecond", expandable: true)
            .Mention(42, "User & Co")
            .Build();

        Assert.Equal(
            "<blockquote expandable>First\nSecond</blockquote><a href=\"tg://user?id=42\">User &amp; Co</a>",
            html.Text);
        Assert.Equal(
            "> First\n> Second||[User & Co](tg://user?id=42)",
            markdown.Text);
    }

    [Fact]
    public void TelegramFormattedTextBuilder_RejectsCrossModeComposition()
    {
        var html = TelegramHtml.Create()
            .Bold("HTML")
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            TelegramMarkdownV2.Create().Append(html));

        Assert.Contains("parse mode", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TelegramFormattedTextBuilder_RequiresContent()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            TelegramHtml.Create().Build());

        Assert.Contains("empty", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(" csharp")]
    [InlineData("c sharp")]
    [InlineData("c#")]
    public void TelegramFormattedTextBuilder_RejectsUnsafePreformattedLanguage(string language)
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            TelegramHtml.Create().Pre("code", language));

        Assert.Contains("language", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void TelegramFormattedTextBuilder_RejectsMissingCustomEmojiValues(string value)
    {
        Assert.Throws<ArgumentException>(() =>
            TelegramHtml.Create().CustomEmoji(value, "💎"));
        Assert.Throws<ArgumentException>(() =>
            TelegramHtml.Create().CustomEmoji("123", value));
    }

    [Fact]
    public void TelegramHtml_UnsafeMarkup_IsExplicitAndPreserved()
    {
        var text = TelegramHtml.UnsafeMarkup("<b>Trusted template</b>");

        Assert.Equal(TelegramParseMode.Html, text.ParseMode);
        Assert.Equal("<b>Trusted template</b>", text.Text);
    }
}
