# State And Wizard

State lets a bot remember where a user is in a conversation. TeleFlow separates state name, state data, and wizard history.

## Register State Storage

For local development:

```csharp
builder.Services.AddMemoryStateStorage();
```

This registers:

- `IStateStore`;
- `IStateDataStore`;
- `IStateDataSerializer`;
- `IStateHistoryStore`;
- state middleware.

Memory storage is process-local. Replace it before multi-instance production deployment.

## Simple State Flow

```csharp
public static class RegistrationStates
{
    public const string Name = "registration:name";
    public const string Age = "registration:age";
}

public sealed class RegistrationHandlers
{
    [Command("register")]
    public async Task Start(MessageContext ctx, CancellationToken ct)
    {
        await ctx.State.SetAsync(RegistrationStates.Name, ct);
        await ctx.Message.AnswerAsync("What is your name?", ct);
    }

    [State(RegistrationStates.Name)]
    [HasText]
    public async Task Name(MessageContext ctx, CancellationToken ct)
    {
        await ctx.State.Data.SetAsync("name", ctx.TelegramMessage.Text!, ct);
        await ctx.State.SetAsync(RegistrationStates.Age, ct);
        await ctx.Message.AnswerAsync("How old are you?", ct);
    }

    [State(RegistrationStates.Age)]
    [HasText]
    public async Task Age(MessageContext ctx, CancellationToken ct)
    {
        var name = await ctx.State.Data.GetRequiredAsync<string>("name", ct);
        await ctx.State.ResetAsync(ct);
        await ctx.Message.AnswerAsync($"Registered: {name}, {ctx.TelegramMessage.Text}", ct);
    }
}
```

Use `ResetAsync` when both state and state data should be cleared.

## State API

`ctx.State` separates the current state value from state data:

| API | Behavior |
| --- | --- |
| `GetAsync()` | Reads the current state or returns `null`. |
| `SetAsync(string)` / `SetAsync(State)` | Stores the current state. |
| `IsAsync(State)` | Reads current state and compares it with a typed state value. |
| `ClearAsync()` | Clears only the current state value. |
| `ResetAsync()` | Clears state data first, then clears the current state. |

`ctx.State` caches the current state snapshot for the duration of one update. The first `GetAsync` reads storage, later current-state reads in the same update use the snapshot, and successful `SetAsync` or `ClearAsync` calls update it. Failed storage calls do not update the snapshot. Direct writes through `IStateStore` in the same update are outside this synchronization path; use `ctx.State` inside handlers and middleware.

`ctx.State.Data` stores small JSON-serialized values by string key:

| API | Behavior |
| --- | --- |
| `GetAsync<T>(key)` | Returns `default` when the key is missing. |
| `GetRequiredAsync<T>(key)` | Throws when the key is missing or deserializes to `null`. |
| `SetAsync<T>(key, value)` | Stores a non-null value. |
| `RemoveAsync(key)` | Removes one value. |
| `ClearAsync()` | Clears all data for the current state key. |

`ctx.State.Data` is available only when state data storage and serializer are registered. `AddMemoryStateStorage()` registers both.

## Wizard Navigation

Wizard adds navigation history:

```csharp
public static class TicketStates
{
    public static readonly State Category = State.Create("ticket:category");
    public static readonly State Description = State.Create("ticket:description");
    public static readonly State Confirm = State.Create("ticket:confirm");
}

public sealed class TicketWizard
{
    [Command("ticket")]
    public async Task Start(MessageContext ctx, CancellationToken ct)
    {
        await ctx.Wizard.GoToAsync(TicketStates.Category, ct);
        await ctx.Message.AnswerAsync("Choose category.", ct);
    }

    [State("ticket:category")]
    [HasText]
    public async Task Category(MessageContext ctx, CancellationToken ct)
    {
        await ctx.Wizard.Data.SetAsync("category", ctx.TelegramMessage.Text!, ct);
        await ctx.Wizard.GoToAsync(TicketStates.Description, ct);
        await ctx.Message.AnswerAsync("Describe the issue.", ct);
    }

    [State("ticket:description")]
    [Text("back")]
    public async Task Back(MessageContext ctx, CancellationToken ct)
    {
        await ctx.Wizard.BackAsync(ct);
        await ctx.Message.AnswerAsync("Back to previous step.", ct);
    }
}
```

`BackAsync` requires state history storage. `AddMemoryStateStorage()` registers it.

Wizard API:

| API | Behavior |
| --- | --- |
| `GetCurrentAsync()` | Reads the current wizard state or returns `null`. |
| `Current` | Returns the current state snapshot; throws if no state is active in the current update. |
| `GoToAsync(state)` | Sets the next state and pushes the previous state into history when one existed. |
| `BackAsync()` | Restores the previous state; throws when history is empty. |
| `ResetAsync()` | Clears state data, history, and the current state. |

## Scenes

Scenes give a canonical state prefix and named steps:

```csharp
[Scene("ticket")]
public sealed class TicketScene
{
    public static State Category { get; } = State.Create("ticket:category");

    [Command("ticket")]
    public async Task Start(MessageContext ctx, CancellationToken ct)
    {
        await ctx.Wizard.GoToAsync(Category, ct);
        await ctx.Message.AnswerAsync("Choose category.", ct);
    }

    [SceneStep(nameof(Category))]
    [HasText]
    public Task CategoryStep(MessageContext ctx, CancellationToken ct)
    {
        return ctx.Message.AnswerAsync("Category saved.", ct);
    }
}
```

Scenes are useful when a flow grows and state names need a stable structure.

## State Key

Telegram state keys are created from Telegram context. For message and callback flows this keeps state scoped to the user and chat. Advanced applications can replace `IStateKeyFactory`.

```csharp
builder.Services.AddStateKeyFactory<MyStateKeyFactory>();
```

Use custom keys when business requirements need tenant-aware, bot-aware, or cross-chat state partitioning.
