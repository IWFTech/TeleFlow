# Roles And Chat Members

TeleFlow supports role requirements and chat member update handlers.

## Role Filter

Use `[RequireTelegramRole]` when a handler should be available only to specific Telegram member statuses:

```csharp
[Command("admin")]
[RequireTelegramRole(TelegramMemberStatusSet.IsAdmin)]
public Task Admin(MessageContext ctx, CancellationToken ct)
{
    return ctx.Message.AnswerAsync("Admin panel.", ct);
}
```

Role checks use Telegram member status information. The framework includes resolver and cache services that can be replaced:

```csharp
builder.Services.AddTelegramChatMemberStatusResolver<MyResolver>();
builder.Services.AddTelegramChatMemberStatusCache<MyCache>();
```

The default role cache is controlled through bot options:

```csharp
builder.Services.AddTelegramBot(options =>
{
    options.Token = token;
    options.RoleFilter.CacheEnabled = true;
    options.RoleFilter.CacheTtl = TimeSpan.FromSeconds(30);
});
```

Use a short TTL for Telegram roles unless your product has a stronger consistency model outside Telegram.

## Chat Member Updates

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

## Exact Status Changes

Use `[ChatMemberChanged]` when a specific old/new status pair matters:

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

## When To Use Roles

Use Telegram role filters for Telegram permissions: admins, creators, members, banned users, or restricted users.

Use your own application authorization service when the rule is about your product: paid plans, tenant access, internal staff roles, or support team ownership.
