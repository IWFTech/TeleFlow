# Конфигурация и секреты

TeleFlow не требует специальной системы конфигурации. Framework получает уже resolved values:

```csharp
builder.Services.AddTelegramBot(options =>
{
    options.Token = token;
});
```

Откуда берётся `token` - ответственность приложения. Используй обычную .NET configuration: `appsettings.json`, user secrets, environment variables, command-line arguments или secret storage твоей deployment platform.

## Рекомендуемая policy для токена

Для local development:

- используй user secrets или environment variables;
- не коммить реальные tokens;
- используй `appsettings.Development.json` только для non-secret local defaults.

Для production:

- используй environment variables, secret managers, CI/CD variables, Kubernetes secrets, Docker secrets или platform-specific secret storage;
- не логируй token;
- падай на startup, если token не настроен.

## Plain console app

`TeleFlowApplication.CreateBuilder(...)` отдаёт `IServiceCollection` и намеренно остаётся маленьким. В plain console app `IConfiguration` нужно собрать самому, если хочешь `appsettings.json`.

Установи configuration packages в application project:

```bash
dotnet add package Microsoft.Extensions.Configuration.Json
dotnet add package Microsoft.Extensions.Configuration.EnvironmentVariables
dotnet add package Microsoft.Extensions.Configuration.UserSecrets
```

`appsettings.json`:

```json
{
  "Telegram": {
    "BotToken": ""
  }
}
```

Program:

```csharp
using Microsoft.Extensions.Configuration;
using TeleFlow.Core.Application;
using TeleFlow.Storage.Memory;
using TeleFlow.Telegram;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
    .AddUserSecrets<Program>(optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

var token = configuration["Telegram:BotToken"]
    ?? throw new InvalidOperationException("Telegram:BotToken is not configured.");

var builder = TeleFlowApplication.CreateBuilder(args);

builder.Services.AddTelegramBot(options =>
{
    options.Token = token;
    options.BotUsername = configuration["Telegram:BotUsername"];
});

builder.Services.AddMemoryStateStorage();
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddLongPolling();

await using var app = builder.Build();
await app.RunAsync();
```

Override через environment variable:

```bash
set TELEGRAM__BOTTOKEN=123456:ABC
```

На Linux/macOS:

```bash
export TELEGRAM__BOTTOKEN="123456:ABC"
```

.NET превращает double underscore в `:` в configuration keys.

## User secrets

Для local development:

```bash
dotnet user-secrets init
dotnet user-secrets set "Telegram:BotToken" "123456:ABC"
dotnet user-secrets set "Telegram:BotUsername" "my_bot"
```

User secrets нужны только для development. Это не production secret manager.

## ASP.NET Core webhook app

Если бот работает внутри ASP.NET Core, используй configuration из host builder:

```csharp
using TeleFlow.Telegram;
using TeleFlow.Telegram.Webhooks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTelegramBot(options =>
{
    options.Token = builder.Configuration["Telegram:BotToken"]
        ?? throw new InvalidOperationException("Telegram:BotToken is not configured.");
    options.BotUsername = builder.Configuration["Telegram:BotUsername"];
});

builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddWebhook(options =>
{
    options.Path = builder.Configuration["Telegram:WebhookPath"] ?? "/telegram/webhook";
    options.SecretToken = builder.Configuration["Telegram:WebhookSecret"];
});

var app = builder.Build();

app.MapTelegramWebhook();

await app.RunAsync();
```

## Binding options

В маленьких apps explicit read usually clearer. В больших apps можно bind-ить свой settings object и валидировать его до регистрации TeleFlow:

```csharp
public sealed class TelegramSettings
{
    public string BotToken { get; init; } = string.Empty;

    public string? BotUsername { get; init; }
}
```

```csharp
var telegram = configuration
    .GetRequiredSection("Telegram")
    .Get<TelegramSettings>()
    ?? throw new InvalidOperationException("Telegram configuration section is missing.");

if (string.IsNullOrWhiteSpace(telegram.BotToken))
{
    throw new InvalidOperationException("Telegram:BotToken is not configured.");
}

builder.Services.AddTelegramBot(options =>
{
    options.Token = telegram.BotToken;
    options.BotUsername = telegram.BotUsername;
});
```

Главное правило не меняется: application resolves configuration, затем явно передаёт значения в TeleFlow.

