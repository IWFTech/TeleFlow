# Telegram Client And Schema

TeleFlow exposes Telegram Bot API directly through `ITelegramClient`.

The framework helpers are conveniences. They do not replace the low-level client. If Telegram has a method, the generated client extensions are the place to call it.

## Direct Client Registration

Use `TeleFlow.Telegram` for client-only applications:

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

## Client Through The Framework

`AddTelegramBot(...)` registers the same client for handler applications:

```csharp
builder.Services.AddTelegramBot(options =>
{
    options.Token = token;
    options.Defaults.ParseMode = TelegramParseMode.Html;
});
```

Inside a handler:

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

## Defaults

`TelegramBotDefaults` can configure common request defaults:

```csharp
builder.Services.AddTelegramBot(options =>
{
    options.Token = token;
    options.Defaults.ParseMode = TelegramParseMode.Html;
    options.Defaults.DisableNotification = true;
    options.Defaults.ProtectContent = true;
});
```

Defaults are useful for cross-cutting Telegram request settings. Keep handler-specific values in the handler.

## Custom Transport

Applications can replace the transport:

```csharp
builder.Services.AddTelegramHttpTransport(httpClient);
```

Or with a custom implementation:

```csharp
builder.Services.AddTelegramTransport<MyTelegramTransport>();
```

Use this for tests, custom networking, proxies, diagnostics, or controlled `HttpClient` ownership.

## JSON Options

Telegram JSON options can be replaced:

```csharp
builder.Services.AddTelegramJsonOptions(options =>
{
    options.WriteIndented = false;
});
```

Be careful with JSON customization. Telegram schema serialization is part of the client contract.

## Deep Links

The client package registers `TelegramDeepLinks` on `ITelegramClient.DeepLinks`.

Configure `BotUsername` when you need to build links:

```csharp
builder.Services.AddTelegramBot(options =>
{
    options.Token = token;
    options.BotUsername = "my_bot";
});
```

Then create plain or typed payload links:

```csharp
public sealed record InvitePayload(long TenantId, string Code);

var link = ctx.Bot.DeepLinks.Start(new InvitePayload(42, "support"));
await ctx.Message.AnswerAsync(link.ToString(), ct);
```

Use `StartGroup(...)` when the link should add the bot to a group:

```csharp
var groupLink = ctx.Bot.DeepLinks.StartGroup("tenant-42");
```

The default serializer stores typed payloads as Base64Url JSON. Payloads are validated against Telegram deep-link payload rules. If `BotUsername` is not configured, link creation fails with a configuration error.

You can replace the serializer:

```csharp
builder.Services.AddDeepLinkPayloadSerializer<MyDeepLinkPayloadSerializer>();
```

## Context Media Helpers

Handlers can use generated client methods directly, but common media replies also have context helpers:

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

Use context helpers when the current chat and reply target are obvious. Use `ctx.Bot.*Async` when you need the full Telegram method surface or a different chat target.

## Media Groups

`MediaGroup` is a small builder over Telegram `sendMediaGroup`:

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

Telegram media groups must contain 2 to 10 items. TeleFlow validates that before sending the request.

## Schema Package

`TeleFlow.Telegram.Schema` contains generated Telegram DTOs, method models, and abstractions. Most applications get it transitively through client or framework packages.

Reference it directly only when you intentionally need schema models without the client runtime.
