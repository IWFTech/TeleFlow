using TeleFlow.Annotations;
using TeleFlow.Telegram;
using TeleFlow.Telegram.Schema.Constants;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.ArchitectureTests;

public sealed class KeyboardBuilderTests
{
    [Fact]
    public void InlineKeyboardBuilder_BuildsMixedTypedRawAndUrlRows()
    {
        var markup = InlineKeyboardBuilder.Create()
            .Button(
                "Delete",
                new InlineKeyboardDeleteCallback(42),
                new InlineKeyboardButtonOptions
                {
                    Style = ButtonStyles.Danger,
                    IconCustomEmojiId = "emoji-delete"
                })
            .Button("Raw", "raw:42", new InlineKeyboardButtonOptions { Style = ButtonStyles.Primary })
            .Row()
            .Url("Open", "https://example.com", new InlineKeyboardButtonOptions { Style = ButtonStyles.Success })
            .Build();

        Assert.Equal(2, markup.InlineKeyboard.Count);
        Assert.Equal("del:42", markup.InlineKeyboard[0][0].CallbackData);
        Assert.Equal(ButtonStyles.Danger, markup.InlineKeyboard[0][0].Style);
        Assert.Equal("emoji-delete", markup.InlineKeyboard[0][0].IconCustomEmojiId);
        Assert.Equal("raw:42", markup.InlineKeyboard[0][1].CallbackData);
        Assert.Equal(ButtonStyles.Primary, markup.InlineKeyboard[0][1].Style);
        Assert.Equal("https://example.com", markup.InlineKeyboard[1][0].Url);
        Assert.Equal(ButtonStyles.Success, markup.InlineKeyboard[1][0].Style);
    }

    [Fact]
    public void InlineKeyboardBuilder_RawCallbackData_PreservesExactString()
    {
        var markup = InlineKeyboardBuilder.Create()
            .Button("Opaque", "redis-key:abc123")
            .Build();

        Assert.Equal("redis-key:abc123", markup.InlineKeyboard[0][0].CallbackData);
    }

    [Fact]
    public void InlineKeyboardBuilder_TypedCallbackPayload_BuildsCompactCallbackData()
    {
        var markup = InlineKeyboardBuilder.Create()
            .Button("Delete", new InlineKeyboardDeleteCallback(42))
            .Build();

        Assert.Equal("del:42", markup.InlineKeyboard[0][0].CallbackData);
    }

    [Fact]
    public void InlineKeyboardBuilder_TypedCallbackPayload_UsesRuntimePayloadType()
    {
        object payload = new InlineKeyboardDeleteCallback(42);

        var markup = InlineKeyboardBuilder.Create()
            .Button("Delete", payload)
            .Build();

        Assert.Equal("del:42", markup.InlineKeyboard[0][0].CallbackData);
    }

    [Fact]
    public void InlineKeyboardBuilder_UnannotatedTypedCallbackPayload_FailsClearly()
    {
        var keyboard = InlineKeyboardBuilder.Create()
            .Button("Json", new UnannotatedInlineKeyboardCallback(42));

        var exception = Assert.Throws<InvalidOperationException>(() => keyboard.Build());

        Assert.Contains(nameof(CallbackDataAttribute), exception.Message);
        Assert.Contains("raw string callback data", exception.Message);
    }

    [Fact]
    public void InlineKeyboardBuilder_RawCallbackData_DoesNotRequireCallbackDataAttribute()
    {
        var markup = InlineKeyboardBuilder.Create()
            .Button("Raw", "raw:42")
            .Build();

        Assert.Equal("raw:42", markup.InlineKeyboard[0][0].CallbackData);
    }

    [Fact]
    public void InlineKeyboardBuilder_RejectsRawCallbackDataOverTelegramLimit()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            InlineKeyboardBuilder.Create().Button("Large", new string('x', 65)));

        Assert.Contains("64", exception.Message);
    }

    [Fact]
    public void InlineKeyboardBuilder_RejectsTypedCallbackDataOverTelegramLimit()
    {
        var keyboard = InlineKeyboardBuilder.Create()
            .Button("Large", new LargeInlineKeyboardCallback(new string('x', 80)));

        var exception = Assert.Throws<InvalidOperationException>(() => keyboard.Build());

        Assert.Contains("64", exception.Message);
    }

    [Fact]
    public void InlineKeyboardBuilder_RejectsEmptyKeyboard()
    {
        var keyboard = InlineKeyboardBuilder.Create();

        var exception = Assert.Throws<InvalidOperationException>(() => keyboard.Build());

        Assert.Contains("Inline keyboard must contain at least one button", exception.Message);
    }

    [Fact]
    public void ReplyKeyboard_CreatesMarkupWithRowsAndOptions()
    {
        var keyboard = ReplyKeyboard.Create()
            .Button("Yes")
            .Button(new KeyboardButton { Text = "No" })
            .Row()
            .Button("Cancel")
            .Resize()
            .OneTime()
            .Persistent()
            .Placeholder("Choose")
            .Selective();

        var markup = keyboard.ToMarkup();

        Assert.True(markup.ResizeKeyboard);
        Assert.True(markup.OneTimeKeyboard);
        Assert.True(markup.IsPersistent);
        Assert.True(markup.Selective);
        Assert.Equal("Choose", markup.InputFieldPlaceholder);
        Assert.Equal(2, markup.Keyboard.Count);
        Assert.Equal(["Yes", "No"], markup.Keyboard[0].Select(static button => button.Text));
        Assert.Equal(["Cancel"], markup.Keyboard[1].Select(static button => button.Text));
    }

    [Fact]
    public void ReplyKeyboard_IgnoresEmptyRows_AndDefensivelyCopiesMarkup()
    {
        var keyboard = ReplyKeyboard.Create()
            .Button("First")
            .Row()
            .Row();

        var markup = keyboard.ToMarkup();
        keyboard.Button("Second");

        Assert.Single(markup.Keyboard);
        Assert.Equal(["First"], markup.Keyboard[0].Select(static button => button.Text));
    }

    [Fact]
    public void ReplyKeyboard_RejectsEmptyKeyboard()
    {
        var keyboard = ReplyKeyboard.Create();

        var exception = Assert.Throws<InvalidOperationException>(() => keyboard.ToMarkup());
        Assert.Contains("Reply keyboard must contain at least one button", exception.Message);
    }

    [Fact]
    public void ReplyKeyboard_RejectsInputPlaceholderOverTelegramLimit()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            ReplyKeyboard.Create().Placeholder(new string('a', 65)));

        Assert.Contains("64", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("placeholder", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void KeyboardRemove_CreatesRemoveMarkup()
    {
        var markup = KeyboardRemove.Create()
            .Selective()
            .ToMarkup();

        Assert.True(markup.RemoveKeyboard);
        Assert.True(markup.Selective);
    }

    [Fact]
    public void ForceReplyBuilder_CreatesForceReplyMarkup()
    {
        var markup = ForceReplyBuilder.Create()
            .Placeholder("Name")
            .Selective()
            .ToMarkup();

        Assert.True(markup.ForceReplyValue);
        Assert.True(markup.Selective);
        Assert.Equal("Name", markup.InputFieldPlaceholder);
    }

    [Fact]
    public void ForceReplyBuilder_RejectsInputPlaceholderOverTelegramLimit()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            ForceReplyBuilder.Create().Placeholder(new string('a', 65)));

        Assert.Contains("64", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("placeholder", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [CallbackData("del")]
    private sealed record InlineKeyboardDeleteCallback(int Id);

    [CallbackData("large")]
    private sealed record LargeInlineKeyboardCallback(string Value);

    private sealed record UnannotatedInlineKeyboardCallback(int Id);
}
