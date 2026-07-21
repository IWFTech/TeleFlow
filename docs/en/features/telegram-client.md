# Telegram Client And Schema

TeleFlow exposes Telegram Bot API directly through `ITelegramClient`.

The framework helpers are conveniences. They do not replace the low-level client. If Telegram has a method, the generated client extensions are the place to call it.

## Direct Client Registration

Use the `IWF.TeleFlow.Telegram` package for client-only applications. The C# namespace remains `TeleFlow.Telegram`:

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

## Telegram Test Environment

Telegram's Bot API test environment is separate from production. Create a dedicated test Telegram account and bot, then use that bot's token. Do not reuse a production token.

For framework applications:

```csharp
builder.Services.AddTelegramBot(options =>
{
    options.Token = testBotToken;
    options.Environment = TelegramBotApiEnvironment.Test;
});
```

For client-only applications:

```csharp
services.AddTelegramClient(options =>
{
    options.Token = testBotToken;
    options.Environment = TelegramBotApiEnvironment.Test;
});
```

The client sends every Bot API request through Telegram's test endpoint, including generated methods, long polling, webhook setup, and payment API calls. Production remains the default.

`BaseUrl` stays the API root. Do not put `/bot`, a token, or `/test` into it. For example, with the default root, TeleFlow builds `https://api.telegram.org/bot<TOKEN>/test/getMe`.

This setting only selects Telegram's endpoint. It does not emulate payments or add framework routes for payment updates. In particular, `TelegramAllowedUpdates.Auto` does not currently infer `pre_checkout_query`; configure that update explicitly when using a raw payment-update path.

Telegram notes that test-environment flood limits are not relaxed and can be stricter than production. Keep retry and throttling behavior realistic while testing.

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

## Retry-After Policy

Telegram may return `429 Too Many Requests` with `response_parameters.retry_after` or an HTTP `Retry-After` header. TeleFlow handles this through an explicit bounded policy:

```csharp
builder.Services.AddTelegramBot(options =>
{
    options.Token = token;
    options.RetryAfter = TelegramRetryAfterPolicy.Default;
});
```

`TelegramRetryAfterPolicy.Default` retries one short throttled request and waits only when Telegram asks for a delay up to five seconds. If Telegram asks for a longer delay, returns `429` without retry metadata, or the retry count is exhausted, the client throws `TelegramRetryAfterException`.

Disable automatic waiting when the application wants to own all throttling decisions:

```csharp
builder.Services.AddTelegramBot(options =>
{
    options.Token = token;
    options.RetryAfter = TelegramRetryAfterPolicy.Disabled;
});
```

Or configure a custom bounded policy:

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

TeleFlow does not automatically retry ordinary Bot API failures or network failures for normal client calls. This avoids hidden duplicate sends for non-idempotent methods. If you need broader retry or outgoing rate limiting, put that policy in application code or replace the request executor intentionally.

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

Custom transports return the raw Telegram response body as UTF-8 bytes:

```csharp
public sealed class MyTelegramTransport : ITelegramTransport
{
    public async Task<TelegramTransportResponse> SendAsync(
        TelegramTransportRequest request,
        CancellationToken cancellationToken = default)
    {
        byte[] body = await SendThroughCustomStackAsync(request, cancellationToken);

        return new TelegramTransportResponse(
            statusCode: 200,
            body: body);
    }
}
```

The `string` constructor is still available for simple tests and adapters, but the client pipeline parses bytes directly to avoid rebuilding JSON text before deserialization.

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

## Schema Constants

Telegram object unions stay generated as typed wrapper models. For example, `ChatMember` is still a wrapper over `ChatMemberOwner`, `ChatMemberAdministrator`, `ChatMemberMember`, and the other concrete DTO cases.

String discriminator values are generated separately under `TeleFlow.Telegram.Schema.Constants`:

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

Use these constants when Telegram expects or returns known string literals: member statuses, command scope types, inline result types, passport error sources, chat boost sources, and similar discriminator families.

## Schema Package

The `IWF.TeleFlow.Telegram.Schema` package contains generated Telegram DTOs, method models, and abstractions. Most applications get it transitively through client or framework packages.

Reference it directly only when you intentionally need schema models without the client runtime.
