# Telegram client и schema

TeleFlow даёт прямой доступ к Telegram Bot API через `ITelegramClient`.

Framework helpers - это удобства. Они не заменяют low-level client. Если в Telegram есть method, generated client extensions - место, где его вызывать.

## Прямая регистрация клиента

Для client-only приложений используй package `IWF.TeleFlow.Telegram`. C# namespace остаётся `TeleFlow.Telegram`:

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

## Retry-After policy

Telegram может вернуть `429 Too Many Requests` с `response_parameters.retry_after` или HTTP header `Retry-After`. TeleFlow обрабатывает это через явную bounded policy:

```csharp
builder.Services.AddTelegramBot(options =>
{
    options.Token = token;
    options.RetryAfter = TelegramRetryAfterPolicy.Default;
});
```

`TelegramRetryAfterPolicy.Default` делает один retry короткого throttled request и ждёт только если Telegram попросил задержку до пяти секунд. Если Telegram попросил ждать дольше, вернул `429` без retry metadata или retry count исчерпан, client бросает `TelegramRetryAfterException`.

Отключи automatic waiting, если приложение должно само владеть всеми throttling decisions:

```csharp
builder.Services.AddTelegramBot(options =>
{
    options.Token = token;
    options.RetryAfter = TelegramRetryAfterPolicy.Disabled;
});
```

Или задай custom bounded policy:

```csharp
builder.Services.AddTelegramBot(options =>
{
    options.Token = token;
    options.RetryAfter = TelegramRetryAfterPolicy.Default with
    {
        MaxRetries = 2,
        MaxDelay = TimeSpan.FromSeconds(3)
    };
});
```

TeleFlow не делает automatic retry обычных Bot API failures или network failures для нормальных client calls. Так мы не создаём hidden duplicate sends для non-idempotent methods. Если нужен более широкий retry или outgoing rate limiting, добавляй эту policy в application code или осознанно заменяй request executor.

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

## Константы схемы

Объединения Telegram-объектов остаются типизированными wrapper-моделями. Например, `ChatMember` всё ещё является wrapper над `ChatMemberOwner`, `ChatMemberAdministrator`, `ChatMemberMember` и другими конкретными DTO.

Строковые значения discriminator-полей генерируются отдельно в `TeleFlow.Telegram.Schema.Constants`:

```csharp
using TeleFlow.Telegram.Schema.Abstractions;
using TeleFlow.Telegram.Schema.Constants;
using TeleFlow.Telegram.Schema.Types;

if (chatMember.ChatMemberAdministrator?.Status == ChatMemberStatuses.Administrator)
{
    // Telegram admin.
}

var scope = new BotCommandScopeChatMember
{
    Type = BotCommandScopeTypes.ChatMember,
    ChatId = IntegerString.From(chatId),
    UserId = userId
};
```

Используй эти константы там, где Telegram принимает или возвращает известные строковые значения: статусы участников, типы command scope, типы inline result, источники passport errors, источники chat boost и похожие discriminator-семейства.

## Schema package

Package `IWF.TeleFlow.Telegram.Schema` содержит generated Telegram DTOs, method models и abstractions. Большинство приложений получают его транзитивно через client или framework packages.

Подключай его напрямую только если тебе нужны schema models без client runtime.
