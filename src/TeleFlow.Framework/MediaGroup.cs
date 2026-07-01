using TeleFlow.Telegram.Schema.Abstractions;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram;

public sealed class MediaGroup
{
    private readonly List<InputMediaGroupItem> _items = [];

    private MediaGroup()
    {
    }

    public static MediaGroup Create()
    {
        return new MediaGroup();
    }

    public MediaGroup Photo(
        InputFileString media,
        string? caption = null,
        TelegramParseMode? parseMode = null)
    {
        ArgumentNullException.ThrowIfNull(media);

        return Item(
            InputMediaGroupItem.From(
                new InputMediaPhoto
                {
                    Media = media,
                    Caption = caption,
                    ParseMode = parseMode?.Value
                }));
    }

    public MediaGroup Video(
        InputFileString media,
        string? caption = null,
        TelegramParseMode? parseMode = null)
    {
        ArgumentNullException.ThrowIfNull(media);

        return Item(
            InputMediaGroupItem.From(
                new InputMediaVideo
                {
                    Media = media,
                    Caption = caption,
                    ParseMode = parseMode?.Value
                }));
    }

    public MediaGroup Document(
        InputFileString media,
        string? caption = null,
        TelegramParseMode? parseMode = null)
    {
        ArgumentNullException.ThrowIfNull(media);

        return Item(
            InputMediaGroupItem.From(
                new InputMediaDocument
                {
                    Media = media,
                    Caption = caption,
                    ParseMode = parseMode?.Value
                }));
    }

    public MediaGroup Audio(
        InputFileString media,
        string? caption = null,
        TelegramParseMode? parseMode = null)
    {
        ArgumentNullException.ThrowIfNull(media);

        return Item(
            InputMediaGroupItem.From(
                new InputMediaAudio
                {
                    Media = media,
                    Caption = caption,
                    ParseMode = parseMode?.Value
                }));
    }

    public MediaGroup LivePhoto(
        InputFileString media,
        InputFileString photo,
        string? caption = null,
        TelegramParseMode? parseMode = null)
    {
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(photo);

        return Item(
            InputMediaGroupItem.From(
                new InputMediaLivePhoto
                {
                    Media = media,
                    Photo = photo,
                    Caption = caption,
                    ParseMode = parseMode?.Value
                }));
    }

    public MediaGroup Item(InputMediaGroupItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        _items.Add(item);
        return this;
    }

    public IReadOnlyList<InputMediaGroupItem> ToMedia()
    {
        if (_items.Count is < 2 or > 10)
        {
            throw new InvalidOperationException("Media group must contain between 2 and 10 items.");
        }

        return _items.ToArray();
    }
}
