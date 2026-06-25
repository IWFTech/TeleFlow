# Telegram client и schema

TeleFlow даёт прямой доступ к Telegram Bot API через `ITelegramClient`.

Framework helpers - это удобства. Они не заменяют low-level client. Если в Telegram есть method, generated client extensions - место, где его вызывать.

## Прямая регистрация клиента

Для client-only приложений используй `TeleFlow.Telegram`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Telegram;

var services = new ServiceCollection();

services.AddTelegramClient(options =>
{
    options.Token = token;
    options.BotUsername = "my_bot";
});

using var provider = services.BuildServiceProvider();
var bot = provider.GetRequiredService<ITelegramClient>();

var me = await bot.GetMeAsync();
```

## Client через framework

`AddTelegramBot(...)` регистрирует тот же client для handler applications:

```csharp
builder.Services.AddTelegramBot(options =>
{
    options.Token = token;
    options.Defaults.ParseMode = TelegramParseMode.Html;
});
```

Внутри handler:

```csharp
using TeleFlow.Telegram.Schema.Abstractions;

[Command("photo")]
public Task Photo(MessageContext ctx, CancellationToken ct)
{
    return ctx.Bot.SendPhotoAsync(
        chatId: IntegerString.From(ctx.TelegramChat.Id),
        photo: InputFileString.From("https://example.com/cat.jpg"),
        caption: "<b>Photo</b>",
        cancellationToken: ct);
}
```

## Дефолты

`TelegramBotDefaults` задаёт common request defaults:

```csharp
builder.Services.AddTelegramBot(options =>
{
    options.Token = token;
    options.Defaults.ParseMode = TelegramParseMode.Html;
    options.Defaults.DisableNotification = true;
    options.Defaults.ProtectContent = true;
});
```

Defaults удобны для cross-cutting Telegram request settings. Handler-specific values держи в handler.

## Собственный transport

Transport можно заменить:

```csharp
builder.Services.AddTelegramHttpTransport(httpClient);
```

Или через свою implementation:

```csharp
builder.Services.AddTelegramTransport<MyTelegramTransport>();
```

Это нужно для tests, custom networking, proxies, diagnostics или контролируемого `HttpClient` ownership.

## JSON options

Telegram JSON options можно заменить:

```csharp
builder.Services.AddTelegramJsonOptions(options =>
{
    options.WriteIndented = false;
});
```

Будь аккуратен с JSON customization. Telegram schema serialization - часть client contract.

## Deep links

Client package регистрирует `TelegramDeepLinks` в `ITelegramClient.DeepLinks`.

Когда нужно строить links, укажи `BotUsername`:

```csharp
builder.Services.AddTelegramBot(options =>
{
    options.Token = token;
    options.BotUsername = "my_bot";
});
```

После этого можно создавать links с простым или typed payload:

```csharp
public sealed record InvitePayload(long TenantId, string Code);

var link = ctx.Bot.DeepLinks.Start(new InvitePayload(42, "support"));
await ctx.Message.AnswerAsync(link.ToString(), ct);
```

Для добавления бота в группу используй `StartGroup(...)`:

```csharp
var groupLink = ctx.Bot.DeepLinks.StartGroup("tenant-42");
```

Default serializer хранит typed payload как Base64Url JSON. Payload валидируется по правилам Telegram deep links. Если `BotUsername` не настроен, создание link упадёт с configuration error.

Serializer можно заменить:

```csharp
builder.Services.AddDeepLinkPayloadSerializer<MyDeepLinkPayloadSerializer>();
```

## Media helpers в context

В handlers можно напрямую вызывать generated client methods, но для частых media replies есть context helpers:

```csharp
using TeleFlow.Telegram.Schema.Abstractions;

[Command("photo")]
public Task Photo(MessageContext ctx, CancellationToken ct)
{
    return ctx.Message.AnswerPhotoAsync(
        InputFileString.From("https://example.com/photo.jpg"),
        caption: "Photo",
        cancellationToken: ct);
}

[HasDocument]
public Task Document(MessageContext ctx, CancellationToken ct)
{
    return ctx.Message.ReplyDocumentAsync(
        InputFileString.From(ctx.TelegramMessage.Document!.FileId),
        caption: "Received.",
        cancellationToken: ct);
}
```

Используй context helpers, когда current chat и reply target очевидны. Используй `ctx.Bot.*Async`, когда нужен полный Telegram method surface или другой chat target.

## Media groups

`MediaGroup` - небольшой builder поверх Telegram `sendMediaGroup`:

```csharp
using TeleFlow.Telegram.Schema.Abstractions;

[Command("album")]
public Task Album(MessageContext ctx, CancellationToken ct)
{
    var media = MediaGroup.Create()
        .Photo(InputFileString.From("https://example.com/a.jpg"), caption: "Album")
        .Photo(InputFileString.From("https://example.com/b.jpg"));

    return ctx.Bot.SendMediaGroupAsync(
        IntegerString.From(ctx.TelegramChat.Id),
        media,
        cancellationToken: ct);
}
```

Telegram media group должен содержать от 2 до 10 элементов. TeleFlow валидирует это до отправки request.

## Schema package

`TeleFlow.Telegram.Schema` содержит generated Telegram DTOs, method models и abstractions. Большинство приложений получают его транзитивно через client или framework packages.

Подключай его напрямую только если тебе нужны schema models без client runtime.
