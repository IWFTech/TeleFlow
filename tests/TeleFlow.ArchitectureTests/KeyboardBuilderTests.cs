using TeleFlow.Telegram;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.ArchitectureTests;

public sealed class KeyboardBuilderTests
{
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
}
