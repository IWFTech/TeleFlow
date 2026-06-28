using TeleFlow.Annotations;
using TeleFlow.Core.Callbacks;
using TeleFlow.Telegram;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.ArchitectureTests;

public sealed class KeyboardBuilderTests
{
    [Fact]
    public void InlineKeyboard_CreatesMarkupWithTypedStringAndUrlButtons()
    {
        var serializer = new TypeRecordingCallbackDataSerializer();
        IInlineKeyboardCallback callback = new InlineKeyboardDeleteCallback(42);

        var markup = InlineKeyboard.Create()
            .Button("Delete", callback)
            .Button("Raw", "raw:42")
            .Row()
            .Url("Open", "https://example.com")
            .ToMarkup(serializer);

        Assert.Equal(2, markup.InlineKeyboard.Count);
        Assert.Equal("InlineKeyboardDeleteCallback:42", markup.InlineKeyboard[0][0].CallbackData);
        Assert.Equal("raw:42", markup.InlineKeyboard[0][1].CallbackData);
        Assert.Equal("https://example.com", markup.InlineKeyboard[1][0].Url);
        Assert.Equal([typeof(InlineKeyboardDeleteCallback)], serializer.SerializedPayloadTypes);
    }

    [Fact]
    public void InlineKeyboard_TypedCallbackButton_UsesRuntimePayloadType()
    {
        var serializer = new TypeRecordingCallbackDataSerializer();
        object callback = new InlineKeyboardDeleteCallback(7);

        var markup = InlineKeyboard.Create()
            .Button("Delete", callback)
            .ToMarkup(serializer);

        Assert.Equal("InlineKeyboardDeleteCallback:7", markup.InlineKeyboard[0][0].CallbackData);
        Assert.Equal([typeof(InlineKeyboardDeleteCallback)], serializer.SerializedPayloadTypes);
    }

    [Fact]
    public void InlineKeyboard_TypedCallbackData_PreservesTelegramByteLimitValidation()
    {
        var serializer = new JsonCallbackDataSerializer(TelegramJsonOptions.CreateDefault());
        var keyboard = InlineKeyboard.Create()
            .Button("Large", new LargeInlineKeyboardCallback(new string('x', 80)));

        var exception = Assert.Throws<InvalidOperationException>(() => keyboard.ToMarkup(serializer));

        Assert.Contains("64 UTF-8 bytes", exception.Message);
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

    private interface IInlineKeyboardCallback
    {
        int Id { get; }
    }

    private sealed record InlineKeyboardDeleteCallback(int Id) : IInlineKeyboardCallback;

    [CallbackData("large")]
    private sealed record LargeInlineKeyboardCallback(string Value);

    private sealed class TypeRecordingCallbackDataSerializer : ICallbackDataSerializer
    {
        public List<Type> SerializedPayloadTypes { get; } = [];

        public string Serialize<TPayload>(TPayload payload)
        {
            SerializedPayloadTypes.Add(typeof(TPayload));

            return payload is IInlineKeyboardCallback callback
                ? $"{typeof(TPayload).Name}:{callback.Id}"
                : typeof(TPayload).Name;
        }

        public TPayload Deserialize<TPayload>(string serializedPayload)
        {
            throw new NotSupportedException();
        }
    }
}
