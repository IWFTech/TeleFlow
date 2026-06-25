# Roles и chat members

TeleFlow поддерживает role requirements и chat member update handlers.

## Role filter

Используй `[RequireTelegramRole]`, когда handler должен быть доступен только конкретным Telegram member statuses:

```csharp
[Command("admin")]
[RequireTelegramRole(TelegramMemberStatusSet.IsAdmin)]
public Task Admin(MessageContext ctx, CancellationToken ct)
{
    return ctx.Message.AnswerAsync("Admin panel.", ct);
}
```

Role checks используют Telegram member status information. Framework содержит resolver и cache services, которые можно заменить:

```csharp
builder.Services.AddTelegramChatMemberStatusResolver<MyResolver>();
builder.Services.AddTelegramChatMemberStatusCache<MyCache>();
```

Default role cache управляется через bot options:

```csharp
builder.Services.AddTelegramBot(options =>
{
    options.Token = token;
    options.RoleFilter.CacheEnabled = true;
    options.RoleFilter.CacheTtl = TimeSpan.FromSeconds(30);
});
```

Для Telegram roles лучше использовать короткий TTL, если у продукта нет более сильной consistency model вне Telegram.

## Chat member updates

Handle chat member updates:

```csharp
using TeleFlow.Telegram.Schema.Abstractions;

[ChatMemberUpdated]
[ChatMemberTransition(TelegramMemberTransition.Join)]
public Task UserJoined(ChatMemberUpdatedContext ctx, CancellationToken ct)
{
    return ctx.Bot.SendMessageAsync(
        chatId: IntegerString.From(ctx.TelegramChat.Id),
        text: $"{ctx.Member.FirstName} joined.",
        cancellationToken: ct);
}
```

Handle bot membership updates:

```csharp
[MyChatMemberUpdated]
[ChatMemberTransition(TelegramMemberTransition.Promoted)]
public Task BotPromoted(ChatMemberUpdatedContext ctx, CancellationToken ct)
{
    return Task.CompletedTask;
}
```

## Точные смены статуса

Используй `[ChatMemberChanged]`, когда важна конкретная old/new status pair:

```csharp
[ChatMemberUpdated]
[ChatMemberChanged(
    TelegramMemberStatusSet.IsNotMember,
    TelegramMemberStatusSet.IsMember)]
public Task BecameMember(ChatMemberUpdatedContext ctx, CancellationToken ct)
{
    return Task.CompletedTask;
}
```

## Когда использовать roles

Telegram role filters подходят для Telegram permissions: admins, creators, members, banned users или restricted users.

Свой application authorization service нужен, когда правило связано с продуктом: paid plans, tenant access, internal staff roles или support team ownership.
