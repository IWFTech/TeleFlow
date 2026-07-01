# Справочник атрибутов

Эта страница кратко описывает public handler metadata attributes.

## Route attributes

| Attribute | Target | Purpose |
| --- | --- | --- |
| `[Message]` | class, method | Message handlers без более specific route. |
| `[Command("start")]` | class, method | Matches Telegram commands. |
| `[CommandTemplate("ticket {id:long}")]` | class, method | Command routes с template values. |
| `[CommandRegex("pattern")]` | class, method | Command routes с regex. |
| `[Text("value")]` | class, method | Matches text messages. |
| `[TextTemplate("order {id:long}")]` | class, method | Text с template values. |
| `[TextRegex("pattern")]` | class, method | Text с regex. |
| `[Callback]` | class, method | Raw callback handlers. |
| `[Callback<TPayload>]` | class, method | Typed callback handlers. |
| `[ChatMemberUpdated]` | class, method | Handles `chat_member` updates. |
| `[MyChatMemberUpdated]` | class, method | Handles `my_chat_member` updates. |

## Настройки route attributes

`[Command]`, `[CommandTemplate]` и `[CommandRegex]` по умолчанию работают с `/` commands и case-insensitive matching:

```csharp
[Command("start")]
public Task Start(MessageContext ctx, CancellationToken ct) { ... }
```

Prefixes можно настроить, если бот принимает command-like text из другого источника:

```csharp
[Command("start", Prefixes = new[] { "/", "!" }, AllowSpaceAfterPrefix = true)]
public Task Start(MessageContext ctx, CancellationToken ct) { ... }
```

`PrefixMode` нужен, когда текст без prefix должен обрабатываться как command-shaped input:

```csharp
[CommandTemplate(
    "ticket {id:long}",
    PrefixMode = CommandPrefixMode.Optional)]
public Task Ticket(MessageContext ctx, long id, CancellationToken ct) { ... }
```

Доступные значения `CommandPrefixMode`:

| Value | Behavior |
| --- | --- |
| `Required` | Требует один из настроенных prefixes. Это значение по умолчанию. |
| `Optional` | Принимает настроенный prefix или текст без prefix. |
| `NoPrefix` | Принимает только текст без prefix. Не комбинируй с `Prefixes`. |

`[Text]` поддерживает явный match mode:

```csharp
[Text("support", TextMatchMode.Contains, ignoreCase: true)]
public Task Support(MessageContext ctx, CancellationToken ct) { ... }
```

Доступные значения `TextMatchMode`: `Equals`, `StartsWith`, `Contains`.

Templates лучше, когда нужны typed route values:

```csharp
[CommandTemplate("ticket {id:long}")]
public Task Ticket(MessageContext ctx, long id, CancellationToken ct) { ... }
```

Regex стоит использовать только когда форма input плохо выражается template.

## Message content filters

| Attribute | Purpose |
| --- | --- |
| `[HasText]` | Message has text. |
| `[HasCaption]` | Message has caption. |
| `[HasPhoto]` | Message has photo. |
| `[HasDocument]` | Message has document. |
| `[HasAudio]` | Message has audio. |
| `[HasVoice]` | Message has voice. |
| `[HasVideo]` | Message has video. |
| `[HasVideoNote]` | Message has video note. |
| `[HasAnimation]` | Message has animation. |
| `[HasSticker]` | Message has sticker. |
| `[HasContact]` | Message has contact. |
| `[HasDice]` | Message has dice. |
| `[HasLocation]` | Message has location. |
| `[HasPoll]` | Message has poll. |
| `[HasVenue]` | Message has venue. |
| `[HasMessageThread]` | Message belongs to a message thread. |
| `[IsReply]` | Message is a reply. |
| `[ReplyToBot]` | Message replies to the bot. |

## Sender and chat filters

| Attribute | Purpose |
| --- | --- |
| `[ChatType(...)]` | Limits handler to private, group, supergroup, channel, or sender chats. |
| `[ChatId(...)]` | Limits handler to chat ids. Ноль отклоняется. |
| `[ChatUsername(...)]` | Limits handler to chat usernames. |
| `[MessageThreadId(...)]` | Limits handler to message thread ids. |
| `[FromUser(...)]` | Limits handler to positive user ids. |
| `[FromBot]` | Filters messages from bot users. |
| `[FromPremiumUser]` | Filters premium users. |
| `[RequireTelegramRole(...)]` | Requires Telegram member statuses. |

## Callback attributes

| Attribute | Purpose |
| --- | --- |
| `[CallbackData("prefix")]` | Marks typed callback payload and compact prefix. |
| `[CallbackDataPrefix("prefix")]` | Filters raw or typed callbacks by callback data prefix. |
| `[HasCallbackData]` | Requires callback data. |
| `[AutoAnswerCallback]` | Auto-answers matching callback handlers. |

## State and scene attributes

| Attribute | Purpose |
| --- | --- |
| `[State("state-id")]` | Matches handlers only in state. |
| `[State<TStateGroup>("Name")]` | Matches state declared by state group. |
| `[StateGroup]` | Marks state group type. |
| `[StateValue("value")]` | Sets custom state segment for state group property. |
| `[Scene("prefix")]` | Marks scene and its state prefix. |
| `[SceneStep("StateName")]` | Marks scene step method. |

## Chat member attributes

| Attribute | Purpose |
| --- | --- |
| `[ChatMemberTransition(...)]` | Matches join, leave, promoted, or demoted transitions. |
| `[ChatMemberChanged(old, new)]` | Matches explicit status changes. |

## Error and module attributes

| Attribute | Purpose |
| --- | --- |
| `[Error]` | Catch-all error handler. |
| `[Error<TException>]` | Error handler for specific exception type. |
| `[TelegramModule]` | Marks module type. |
| `[UseFilter<TFilter>]` | Adds custom filter. |

## Base types для custom filter attributes

| Type | Purpose |
| --- | --- |
| `TelegramFilterAttribute<TFilter>` | Base class для parameterized custom filter attributes. |
| `ITelegramFilter<TContext>` | Custom filter contract без attribute metadata. |
| `ITelegramFilter<TContext, TAttribute>` | Custom filter contract с attribute metadata. |

## Рекомендация

Используй самый specific attribute, который ясно выражает route. Templates обычно лучше regex, если input structure подходит. Broad catch-all handlers должны быть intentional last-resort behavior.
