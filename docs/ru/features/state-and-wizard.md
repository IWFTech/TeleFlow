# State и wizard

State позволяет боту помнить, где пользователь находится в conversation. TeleFlow разделяет state name, state data и wizard history.

## Регистрация state storage

Для local development:

```csharp
builder.Services.AddMemoryStateStorage();
```

Это регистрирует:

- `IStateStore`;
- `IStateDataStore`;
- `IStateDataSerializer`;
- `IStateHistoryStore`;
- state middleware.

Memory storage process-local. Перед multi-instance production deployment его нужно заменить.

## Простой state flow

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

Используй `ResetAsync`, когда нужно очистить и state, и state data.

## State API

`ctx.State` разделяет current state value и state data:

| API | Поведение |
| --- | --- |
| `GetAsync()` | Читает current state или возвращает `null`. |
| `SetAsync(string)` / `SetAsync(State)` | Сохраняет current state. |
| `IsAsync(State)` | Читает current state и сравнивает его с typed state value. |
| `ClearAsync()` | Очищает только current state value. |
| `ResetAsync()` | Сначала очищает state data, затем current state. |

`ctx.State` кэширует snapshot текущего state на время одного update. Первый `GetAsync` читает storage, следующие current-state reads в том же update используют snapshot, а успешные `SetAsync` или `ClearAsync` обновляют его. Failed storage calls snapshot не меняют. Прямые записи через `IStateStore` внутри того же update находятся вне этого synchronization path; в handlers и middleware используй `ctx.State`.

`ctx.State.Data` хранит небольшие JSON-serialized values по string key:

| API | Поведение |
| --- | --- |
| `GetAsync<T>(key)` | Возвращает `default`, если key отсутствует. |
| `GetRequiredAsync<T>(key)` | Падает, если key отсутствует или deserializes to `null`. |
| `SetAsync<T>(key, value)` | Сохраняет non-null value. |
| `RemoveAsync(key)` | Удаляет одно значение. |
| `ClearAsync()` | Очищает все data для текущего state key. |

`ctx.State.Data` доступен только когда зарегистрированы state data storage и serializer. `AddMemoryStateStorage()` регистрирует оба.

## Wizard navigation

Wizard добавляет navigation history:

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

`BackAsync` требует state history storage. `AddMemoryStateStorage()` его регистрирует.

Wizard API:

| API | Поведение |
| --- | --- |
| `GetCurrentAsync()` | Читает current wizard state или возвращает `null`. |
| `Current` | Возвращает current state snapshot; падает, если в current update нет active state. |
| `GoToAsync(state)` | Устанавливает следующий state и кладёт previous state в history, если он был. |
| `BackAsync()` | Восстанавливает previous state; падает, если history пустой. |
| `ResetAsync()` | Очищает state data, history и current state. |

## Scenes

Scenes дают canonical state prefix и named steps:

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

Scenes полезны, когда flow растёт и state names нужна стабильная структура.

## State key

Telegram state keys создаются из Telegram context. Для message и callback flows state остаётся scoped to user and chat. Advanced applications могут заменить `IStateKeyFactory`.

```csharp
builder.Services.AddStateKeyFactory<MyStateKeyFactory>();
```

Custom keys нужны, когда business requirements требуют tenant-aware, bot-aware или cross-chat state partitioning.
