namespace TeleFlow.Telegram;

public abstract record TelegramTransportContent;

public sealed record TelegramJsonTransportContent(string Json) : TelegramTransportContent;

public sealed record TelegramMultipartTransportContent(
    IReadOnlyList<TelegramMultipartField> Fields,
    IReadOnlyList<TelegramMultipartFile> Files) : TelegramTransportContent;

public sealed record TelegramMultipartField(string Name, string Value);

public sealed record TelegramMultipartFile(
    string Name,
    string FileName,
    Stream Content,
    string? ContentType = null);
