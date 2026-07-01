# Configuration And Secrets

TeleFlow does not require a special configuration system. The framework receives already resolved values:

```csharp
builder.Services.AddTelegramBot(options =>
{
    options.Token = token;
});
```

Where `token` comes from is application code. Use normal .NET configuration: `appsettings.json`, user secrets, environment variables, command-line arguments, or your deployment platform.

## Recommended Token Policy

For local development:

- use user secrets or environment variables;
- keep real tokens out of committed files;
- use `appsettings.Development.json` only for non-secret local defaults.

For production:

- use environment variables, secret managers, CI/CD variables, Kubernetes secrets, Docker secrets, or platform-specific secret storage;
- do not log the token;
- fail during startup when the token is missing.

## Plain Console App

`TeleFlowApplication.CreateBuilder(...)` exposes `IServiceCollection` and intentionally stays small. In a plain console app, build `IConfiguration` yourself when you want `appsettings.json`.

Install configuration packages in the application project:

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
using TeleFlow.Framework.Application;
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

Environment variable override:

```bash
set TELEGRAM__BOTTOKEN=123456:ABC
```

On Linux/macOS:

```bash
export TELEGRAM__BOTTOKEN="123456:ABC"
```

.NET maps double underscores to `:` in configuration keys.

## User Secrets

For local development:

```bash
dotnet user-secrets init
dotnet user-secrets set "Telegram:BotToken" "123456:ABC"
dotnet user-secrets set "Telegram:BotUsername" "my_bot"
```

User secrets are for development only. They are not a production secret manager.

## ASP.NET Core Webhook App

When the bot runs inside ASP.NET Core, use the host builder's configuration:

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

## Binding Options

For small apps, reading values explicitly is clearer. For larger apps, bind your own settings object and validate it before registering TeleFlow:

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

The important rule is the same: configuration is resolved by the application, then passed explicitly to TeleFlow.

