# Quickstart

На этой странице мы соберём минимальный long polling bot и разберём, что делает каждая строка. Пример маленький, но использует ту же application model, что и большой TeleFlow-проект.

## Что получится

Бот будет:

- запускаться по `/start`;
- отвечать на текстовые сообщения;
- использовать dependency injection;
- использовать generated handler registration;
- получать updates через long polling;
- иметь state storage для следующих шагов.

## Требования

- .NET SDK с поддержкой `net10.0`.
- Telegram bot token от BotFather.

Передай token перед запуском.

Linux или macOS:

```bash
export TELEFLOW_BOT_TOKEN=123456:token
```

PowerShell:

```powershell
$env:TELEFLOW_BOT_TOKEN = "123456:token"
```

В quickstart используется environment variable, потому что это самый короткий безопасный пример. Для `appsettings.json`, user secrets и ASP.NET Core configuration смотри [Конфигурация и секреты](configuration.md).

## Создай проект

TeleFlow сейчас опубликован как public alpha, поэтому установка packages использует `--prerelease`.

```bash
dotnet new console -n EchoBot
cd EchoBot
dotnet add package IWF.TeleFlow.Framework.LongPolling --prerelease
dotnet add package IWF.TeleFlow.Generators --prerelease
dotnet add package IWF.TeleFlow.Storage.Memory --prerelease
```

В project file держи generator приватной зависимостью:

```xml
<PackageReference Include="IWF.TeleFlow.Generators" Version="..." PrivateAssets="all" />
```

`IWF.TeleFlow.Generators` запускается во время компиляции. Это не runtime-зависимость твоего бота.

## Program.cs

```csharp
using TeleFlow.Annotations;
using TeleFlow.Framework.Application;
using TeleFlow.Storage.Memory;
using TeleFlow.Telegram;

var token = Environment.GetEnvironmentVariable("TELEFLOW_BOT_TOKEN")
    ?? throw new InvalidOperationException("TELEFLOW_BOT_TOKEN is not set.");

var builder = TeleFlowApplication.CreateBuilder(args);

// Включаем логгирование
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
    [Message] // Говорим, что нам нужен текст из апдейта сообщения
    [HasText]
    public Task Handle(MessageContext ctx, CancellationToken ct)
    {
        var text = ctx.TelegramMessage.Text ?? string.Empty;
        return ctx.Message.ReplyAsync($"Echo: {text}", ct);
    }
}
```

Запусти:

```bash
dotnet run
```

Открой бота в Telegram, отправь `/start`, потом любое текстовое сообщение.

## Что делает каждая регистрация

```csharp
builder.Services.AddTelegramBot(options => options.Token = token);
```

Регистрирует Telegram framework services и низкоуровневый `ITelegramClient`. Этот вызов должен быть раньше регистрации handlers и framework transports.

```csharp
builder.Services.AddMemoryStateStorage();
```

Регистрирует process-local state storage, state data storage, state history storage, serializer и state middleware. Это подходит для local development и single-process examples. Для multi-process production storage нужно заменить.

```csharp
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
```

Регистрирует handlers из generated metadata. Это рекомендуемый путь для приложений с несколькими handlers. Если application project не ссылается на `IWF.TeleFlow.Generators`, startup падает с понятной ошибкой вместо silent fallback на reflection.

```csharp
builder.Services.AddLongPolling();
```

Регистрирует framework long polling update source. Long polling - самый простой transport для local development и небольших deployment.

## Правила handler в этом примере

Handlers - обычные классы. Method handler может принимать:

- TeleFlow context, например `MessageContext` или `CallbackQueryContext`;
- `CancellationToken`;
- services из dependency injection.

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

Сервисы регистрируются до `Build()`:

```csharp
builder.Services.AddSingleton<ProfileRepository>();
```

## Прямые вызовы Telegram Bot API

Framework не прячет Telegram от тебя. Используй `ctx.Bot`, когда нужен низкоуровневый Bot API:

```csharp
[Command("me")]
public async Task Me(MessageContext ctx, CancellationToken ct)
{
    var me = await ctx.Bot.GetMeAsync(ct);
    await ctx.Message.AnswerAsync($"Bot username: @{me.Username}", ct);
}
```

Generated `*Async` методы соответствуют Telegram Bot API methods.

## Следующие шаги

- Добавить кнопки: [Callbacks and keyboards](../features/callbacks-and-keyboards.md)
- Добавить multi-step forms: [State and wizard](../features/state-and-wizard.md)
- Разделить реальный проект: [Recommended paths](recommended-paths.md)
- Понять generated registration: [Generated registration](../advanced/generated-registration.md)
