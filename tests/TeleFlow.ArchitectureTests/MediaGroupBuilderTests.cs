using TeleFlow.Telegram;
using TeleFlow.Telegram.Schema.Abstractions;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.ArchitectureTests;

public sealed class MediaGroupBuilderTests
{
    [Fact]
    public void MediaGroup_CreatesSupportedMediaItems()
    {
        var media = MediaGroup.Create()
            .Photo("photo-id", caption: "photo", parseMode: TelegramParseMode.Html)
            .Video("video-id", caption: "video", parseMode: TelegramParseMode.MarkdownV2)
            .Document("document-id", caption: "document")
            .Audio("audio-id", caption: "audio")
            .LivePhoto("live-video-id", "live-photo-id", caption: "live")
            .ToMedia();

        Assert.Equal(5, media.Count);

        Assert.True(media[0].TryGetInputMediaPhoto(out var photo));
        Assert.Equal("photo-id", photo?.Media.String);
        Assert.Equal("photo", photo?.Caption);
        Assert.Equal("HTML", photo?.ParseMode);

        Assert.True(media[1].TryGetInputMediaVideo(out var video));
        Assert.Equal("video-id", video?.Media.String);
        Assert.Equal("video", video?.Caption);
        Assert.Equal("MarkdownV2", video?.ParseMode);

        Assert.True(media[2].TryGetInputMediaDocument(out var document));
        Assert.Equal("document-id", document?.Media.String);
        Assert.Equal("document", document?.Caption);

        Assert.True(media[3].TryGetInputMediaAudio(out var audio));
        Assert.Equal("audio-id", audio?.Media.String);
        Assert.Equal("audio", audio?.Caption);

        Assert.True(media[4].TryGetInputMediaLivePhoto(out var livePhoto));
        Assert.Equal("live-video-id", livePhoto?.Media.String);
        Assert.Equal("live-photo-id", livePhoto?.Photo.String);
        Assert.Equal("live", livePhoto?.Caption);
    }

    [Fact]
    public void MediaGroup_AcceptsRawGeneratedMediaItem()
    {
        var media = MediaGroup.Create()
            .Item(InputMediaGroupItem.From(new InputMediaPhoto { Media = InputFileString.From("first") }))
            .Item(InputMediaGroupItem.From(new InputMediaPhoto { Media = InputFileString.From("second") }))
            .ToMedia();

        Assert.Equal(2, media.Count);
        Assert.True(media[0].TryGetInputMediaPhoto(out var first));
        Assert.Equal("first", first?.Media.String);
    }

    [Fact]
    public void MediaGroup_DefensivelyCopiesItems()
    {
        var group = MediaGroup.Create()
            .Photo("first")
            .Photo("second");

        var media = group.ToMedia();
        group.Photo("third");

        Assert.Equal(2, media.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(11)]
    public void MediaGroup_ValidatesTelegramItemLimit(int count)
    {
        var group = MediaGroup.Create();
        for (var i = 0; i < count; i++)
        {
            group.Photo($"photo-{i}");
        }

        var exception = Assert.Throws<InvalidOperationException>(() => group.ToMedia());
        Assert.Contains("Media group must contain between 2 and 10 items", exception.Message);
    }

    [Fact]
    public void MediaGroup_ParseModeNoneSuppressesParseMode()
    {
        var media = MediaGroup.Create()
            .Photo("first", caption: "first", parseMode: TelegramParseMode.None)
            .Photo("second")
            .ToMedia();

        Assert.True(media[0].TryGetInputMediaPhoto(out var photo));
        Assert.Null(photo?.ParseMode);
    }
}
