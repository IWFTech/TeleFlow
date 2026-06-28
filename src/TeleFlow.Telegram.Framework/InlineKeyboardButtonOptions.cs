namespace TeleFlow.Telegram;

public sealed record class InlineKeyboardButtonOptions
{
    public string? Style { get; init; }

    public string? IconCustomEmojiId { get; init; }
}
