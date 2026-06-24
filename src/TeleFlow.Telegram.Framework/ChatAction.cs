namespace TeleFlow.Telegram;

public readonly record struct ChatAction
{
    private ChatAction(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public static ChatAction Typing { get; } = new("typing");

    public static ChatAction UploadPhoto { get; } = new("upload_photo");

    public static ChatAction RecordVideo { get; } = new("record_video");

    public static ChatAction UploadVideo { get; } = new("upload_video");

    public static ChatAction RecordVoice { get; } = new("record_voice");

    public static ChatAction UploadVoice { get; } = new("upload_voice");

    public static ChatAction UploadDocument { get; } = new("upload_document");

    public static ChatAction ChooseSticker { get; } = new("choose_sticker");

    public static ChatAction FindLocation { get; } = new("find_location");

    public static ChatAction RecordVideoNote { get; } = new("record_video_note");

    public static ChatAction UploadVideoNote { get; } = new("upload_video_note");

    public static ChatAction Custom(string value)
    {
        return new ChatAction(value);
    }

    public override string ToString()
    {
        return Value;
    }
}
