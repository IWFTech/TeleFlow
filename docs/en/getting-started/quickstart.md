# Quickstart

This page builds a minimal long polling bot and explains what each line is doing. It is intentionally small, but it uses the same application model as a larger TeleFlow project.

## What You Will Build

The bot will:

- start with `/start`;
- answer text messages;
- use dependency injection;
- use generated handler registration;
- receive updates through long polling;
- keep state storage available for the next steps.

## Requirements

- .NET SDK that supports `net10.0`.
- A Telegram bot token from BotFather.

Set the token before running the app.

Linux or macOS:

```bash
export TELEFLOW_BOT_TOKEN=123456:token
```

PowerShell:

```powershell
$env:TELEFLOW_BOT_TOKEN = "123456:token"
```

This quickstart uses an environment variable because it is the shortest safe example. For `appsettings.json`, user secrets, and ASP.NET Core configuration, see [Configuration and secrets](configuration.md).

## Create The Project

TeleFlow is currently published as a public alpha, so package installation uses `--prerelease`.

```bash
dotnet new console -n EchoBot
cd EchoBot
dotnet add package IWF.TeleFlow.Telegram.Framework.LongPolling --prerelease
dotnet add package IWF.TeleFlow.Generators --prerelease
dotnet add package IWF.TeleFlow.Storage.Memory --prerelease
```

Keep the generator private in the project file:

```xml
<PackageReference Include="IWF.TeleFlow.Generators" Version="..." PrivateAssets="all" />
```

`IWF.TeleFlow.Generators` runs during compilation. It is not part of the runtime dependency surface of your bot.

## Program.cs

```csharp
using TeleFlow.Annotations;
using TeleFlow.Core.Application;
using TeleFlow.Storage.Memory;
using TeleFlow.Telegram;

var token = Environment.GetEnvironmentVariable("TELEFLOW_BOT_TOKEN")
    ?? throw new InvalidOperationException("TELEFLOW_BOT_TOKEN is not set.");

var builder = TeleFlowApplication.CreateBuilder(args);

// Enable logging
builder.Services.AddLogging(logging =>
{
    logging.SetMinimumLevel(LogLevel.Debug);
    logging.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss ";
    });
});

builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddMemoryStateStorage();
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddLongPolling();

await using var app = builder.Build();
await app.RunAsync();

public sealed class StartHandler
{
    [Command("start")]
    public Task Handle(MessageContext ctx, CancellationToken ct)
    {
        return ctx.Message.AnswerAsync("Send me a message.", ct);
    }
}

public sealed class EchoHandler
{
    [Message]
    [HasText]
    public Task Handle(MessageContext ctx, CancellationToken ct)
    {
        var text = ctx.TelegramMessage.Text ?? string.Empty;
        return ctx.Message.ReplyAsync($"Echo: {text}", ct);
    }
}
```

Run it:

```bash
dotnet run
```

Open the bot in Telegram, send `/start`, then send any text message.

## What Each Registration Does

```csharp
builder.Services.AddTelegramBot(options => options.Token = token);
```

Registers the Telegram framework services and the low-level `ITelegramClient`. This must be called before handler registration and before framework transports.

```csharp
builder.Services.AddMemoryStateStorage();
```

Registers process-local state storage, state data storage, state history storage, serializer, and state middleware. It is good for local development and single-process examples. Replace it for multi-process production deployments.

```csharp
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
```

Registers handlers from generated metadata. This is the recommended path for applications with multiple handlers. If the application project does not reference `IWF.TeleFlow.Generators`, startup fails clearly instead of falling back to reflection.

```csharp
builder.Services.AddLongPolling();
```

Registers the framework long polling update source. Long polling is the simplest transport for local development and small deployments.

## Handler Rules In This Example

Handlers are ordinary classes. A handler method can receive:

- a TeleFlow context such as `MessageContext` or `CallbackQueryContext`;
- `CancellationToken`;
- services from dependency injection.

```csharp
public sealed class ProfileHandler
{
    private readonly ProfileRepository _profiles;

    public ProfileHandler(ProfileRepository profiles)
    {
        _profiles = profiles;
    }

    [Command("profile")]
    public async Task Handle(MessageContext ctx, CancellationToken ct)
    {
        var profile = await _profiles.GetAsync(ctx.Sender?.Id, ct);
        await ctx.Message.AnswerAsync($"Profile: {profile.DisplayName}", ct);
    }
}
```

Register your own services before building the app:

```csharp
builder.Services.AddSingleton<ProfileRepository>();
```

## Direct Telegram Bot API Calls

The framework does not hide Telegram from you. Use `ctx.Bot` when you need the low-level Bot API:

```csharp
[Command("me")]
public async Task Me(MessageContext ctx, CancellationToken ct)
{
    var me = await ctx.Bot.GetMeAsync(ct);
    await ctx.Message.AnswerAsync($"Bot username: @{me.Username}", ct);
}
```

The generated `*Async` methods map to Telegram Bot API methods.

## Next Steps

- Add buttons: [Callbacks and keyboards](../features/callbacks-and-keyboards.md)
- Add multi-step forms: [State and wizard](../features/state-and-wizard.md)
- Split a real project: [Recommended paths](recommended-paths.md)
- Understand generated registration: [Generated registration](../advanced/generated-registration.md)
