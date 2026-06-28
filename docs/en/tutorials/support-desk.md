# Tutorial: Support Desk Bot

This tutorial shows a realistic TeleFlow bot shape. The bot lets a user create a support ticket and lets an admin take or resolve it through inline buttons.

It demonstrates:

- application bootstrap;
- dependency injection;
- repositories and services;
- command handlers;
- state and state data;
- typed callbacks;
- inline keyboards;
- direct `ctx.Bot.*Async` calls;
- cancellation tokens.

The storage here is in-memory because the goal is framework usage, not production persistence.

## Project Packages

```xml
<PackageReference Include="IWF.TeleFlow.Telegram.Framework.LongPolling" Version="..." />
<PackageReference Include="IWF.TeleFlow.Generators" Version="..." PrivateAssets="all" />
<PackageReference Include="IWF.TeleFlow.Storage.Memory" Version="..." />
<PackageReference Include="Microsoft.Extensions.Logging.Console" Version="..." />
```

## Program.cs

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeleFlow.Annotations;
using TeleFlow.Core.Application;
using TeleFlow.Storage.Memory;
using TeleFlow.Telegram;
using TeleFlow.Telegram.Schema.Abstractions;

var token = Environment.GetEnvironmentVariable("TELEFLOW_BOT_TOKEN")
    ?? throw new InvalidOperationException("TELEFLOW_BOT_TOKEN is not set.");

var builder = TeleFlowApplication.CreateBuilder(args);

builder.Services.AddLogging(logging => logging.AddConsole());

builder.Services.AddTelegramBot(options => options.Token = token);
builder.Services.AddMemoryStateStorage();
builder.Services.AddTelegramHandlersFromAssembly(typeof(Program).Assembly);
builder.Services.AddLongPolling();

builder.Services.AddSingleton<ITicketRepository, InMemoryTicketRepository>();
builder.Services.AddSingleton<IAdminDirectory, EnvironmentAdminDirectory>();
builder.Services.AddSingleton<TicketNotificationService>();

await using var app = builder.Build();
await app.RunAsync();
```

## Models

```csharp
public enum TicketStatus
{
    Open,
    Taken,
    Resolved
}

public sealed record Ticket(
    long Id,
    long UserId,
    long ChatId,
    string Category,
    string Description,
    TicketStatus Status,
    long? AdminId);

[CallbackData("ticket")]
public sealed record TicketAction(long Id, string Action);
```

## Repositories

```csharp
public interface ITicketRepository
{
    ValueTask<Ticket> CreateAsync(
        long userId,
        long chatId,
        string category,
        string description,
        CancellationToken cancellationToken);

    ValueTask<Ticket?> GetAsync(long id, CancellationToken cancellationToken);

    ValueTask<Ticket> UpdateAsync(
        long id,
        TicketStatus status,
        long? adminId,
        CancellationToken cancellationToken);
}

public sealed class InMemoryTicketRepository : ITicketRepository
{
    private readonly object _lock = new();
    private readonly Dictionary<long, Ticket> _tickets = new();
    private long _nextId;

    public ValueTask<Ticket> CreateAsync(
        long userId,
        long chatId,
        string category,
        string description,
        CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var id = ++_nextId;
            var ticket = new Ticket(
                id,
                userId,
                chatId,
                category,
                description,
                TicketStatus.Open,
                AdminId: null);

            _tickets.Add(id, ticket);
            return ValueTask.FromResult(ticket);
        }
    }

    public ValueTask<Ticket?> GetAsync(long id, CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return ValueTask.FromResult(_tickets.GetValueOrDefault(id));
        }
    }

    public ValueTask<Ticket> UpdateAsync(
        long id,
        TicketStatus status,
        long? adminId,
        CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            var current = _tickets[id];
            var updated = current with
            {
                Status = status,
                AdminId = adminId
            };
            _tickets[id] = updated;
            return ValueTask.FromResult(updated);
        }
    }
}
```

## Admin Directory

```csharp
public interface IAdminDirectory
{
    bool IsAdmin(long userId);

    long? AdminChatId { get; }
}

public sealed class EnvironmentAdminDirectory : IAdminDirectory
{
    private readonly HashSet<long> _adminIds;

    public EnvironmentAdminDirectory()
    {
        _adminIds = (Environment.GetEnvironmentVariable("TELEFLOW_ADMIN_IDS") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(long.Parse)
            .ToHashSet();

        AdminChatId = long.TryParse(
            Environment.GetEnvironmentVariable("TELEFLOW_ADMIN_CHAT_ID"),
            out var chatId)
            ? chatId
            : null;
    }

    public long? AdminChatId { get; }

    public bool IsAdmin(long userId)
    {
        return _adminIds.Contains(userId);
    }
}
```

## Notification Service

```csharp
public sealed class TicketNotificationService
{
    private readonly IAdminDirectory _admins;

    public TicketNotificationService(IAdminDirectory admins)
    {
        _admins = admins;
    }

    public async Task NotifyAdminsAsync(
        ITelegramClient bot,
        Ticket ticket,
        CancellationToken ct)
    {
        if (_admins.AdminChatId is not { } adminChatId)
        {
            return;
        }

        await bot.SendMessageAsync(
            chatId: IntegerString.From(adminChatId),
            text: $"New ticket #{ticket.Id}\n{ticket.Category}\n{ticket.Description}",
            cancellationToken: ct);
    }
}
```

In real application code, inline buttons are usually built in a handler where `ctx.Message.AnswerAsync(...)` can serialize typed callback payloads for you. The service above uses direct `ITelegramClient` calls to show that low-level Bot API access remains available.

## Ticket Wizard

```csharp
public static class TicketStates
{
    public const string Category = "ticket:category";
    public const string Description = "ticket:description";
}

public sealed class TicketCreationHandlers
{
    private readonly ITicketRepository _tickets;
    private readonly TicketNotificationService _notifications;

    public TicketCreationHandlers(
        ITicketRepository tickets,
        TicketNotificationService notifications)
    {
        _tickets = tickets;
        _notifications = notifications;
    }

    [Command("ticket")]
    public async Task Start(MessageContext ctx, CancellationToken ct)
    {
        var keyboard = ReplyKeyboard.Create()
            .Button("Billing")
            .Button("Technical")
            .Row()
            .Button("Other")
            .Resize()
            .OneTime();

        await ctx.State.SetAsync(TicketStates.Category, ct);
        await ctx.Message.AnswerAsync("Choose ticket category.", keyboard, ct);
    }

    [State(TicketStates.Category)]
    [HasText]
    public async Task Category(MessageContext ctx, CancellationToken ct)
    {
        await ctx.State.Data.SetAsync("category", ctx.TelegramMessage.Text!, ct);
        await ctx.State.SetAsync(TicketStates.Description, ct);
        await ctx.Message.AnswerAsync(
            "Describe the issue.",
            ForceReplyBuilder.Create().Placeholder("Short description"),
            ct);
    }

    [State(TicketStates.Description)]
    [HasText]
    public async Task Description(MessageContext ctx, CancellationToken ct)
    {
        var userId = ctx.Sender?.Id
            ?? throw new InvalidOperationException("Ticket creation requires a Telegram user.");

        var category = await ctx.State.Data.GetRequiredAsync<string>("category", ct);
        var description = ctx.TelegramMessage.Text!;

        var ticket = await _tickets.CreateAsync(
            userId,
            ctx.TelegramChat.Id,
            category,
            description,
            ct);

        await ctx.State.ResetAsync(ct);

        await ctx.Message.AnswerAsync(
            $"Ticket #{ticket.Id} created.",
            KeyboardRemove.Create(),
            ct);

        await _notifications.NotifyAdminsAsync(ctx.Bot, ticket, ct);
    }
}
```

## Admin Handlers

```csharp
public sealed class AdminTicketHandlers
{
    private readonly ITicketRepository _tickets;
    private readonly IAdminDirectory _admins;

    public AdminTicketHandlers(
        ITicketRepository tickets,
        IAdminDirectory admins)
    {
        _tickets = tickets;
        _admins = admins;
    }

    [CommandTemplate("ticket {id:long}")]
    public async Task Show(MessageContext ctx, long id, CancellationToken ct)
    {
        if (ctx.Sender is null || !_admins.IsAdmin(ctx.Sender.Id))
        {
            await ctx.Message.AnswerAsync("Admin only.", ct);
            return;
        }

        var ticket = await _tickets.GetAsync(id, ct);
        if (ticket is null)
        {
            await ctx.Message.AnswerAsync("Ticket not found.", ct);
            return;
        }

        var keyboard = InlineKeyboardBuilder.Create()
            .Button(
                "Take",
                new TicketAction(ticket.Id, "take"),
                new InlineKeyboardButtonOptions { Style = "primary" })
            .Button(
                "Resolve",
                new TicketAction(ticket.Id, "resolve"),
                new InlineKeyboardButtonOptions { Style = "success" })
            .Build();

        await ctx.Message.AnswerAsync(
            $"Ticket #{ticket.Id}\nStatus: {ticket.Status}\n{ticket.Description}",
            keyboard,
            ct);
    }

    [Callback<TicketAction>]
    public async Task Action(
        CallbackQueryContext ctx,
        TicketAction payload,
        CancellationToken ct)
    {
        if (!_admins.IsAdmin(ctx.Sender.Id))
        {
            await ctx.Callback.AnswerAsync("Admin only.", showAlert: true, cancellationToken: ct);
            return;
        }

        var status = payload.Action switch
        {
            "take" => TicketStatus.Taken,
            "resolve" => TicketStatus.Resolved,
            _ => TicketStatus.Open
        };

        var ticket = await _tickets.UpdateAsync(payload.Id, status, ctx.Sender.Id, ct);

        await ctx.Callback.AnswerAsync("Saved", ct);
        await ctx.Callback.EditTextAsync(
            $"Ticket #{ticket.Id}\nStatus: {ticket.Status}",
            ct);
    }
}
```

## What This Example Teaches

- Handlers stay small.
- Repositories and services are normal DI services.
- State is used only for the user wizard.
- Admin actions use typed callbacks.
- Telegram API remains available through `ctx.Bot`.
- Memory storage and in-memory repositories are demo choices, not production persistence.

For production, replace storage, add tests, use configuration binding, and document admin access rules.
