# Attributes Reference

This page summarizes public handler metadata attributes.

## Route Attributes

| Attribute | Target | Purpose |
| --- | --- | --- |
| `[Message]` | class, method | Marks message handlers without a more specific route. |
| `[Command("start")]` | class, method | Matches Telegram commands. |
| `[CommandTemplate("ticket {id:long}")]` | class, method | Matches command routes with template values. |
| `[CommandRegex("pattern")]` | class, method | Matches command routes with regex. |
| `[Text("value")]` | class, method | Matches text messages. |
| `[TextTemplate("order {id:long}")]` | class, method | Matches text with template values. |
| `[TextRegex("pattern")]` | class, method | Matches text with regex. |
| `[Callback]` | class, method | Marks raw callback handlers. |
| `[Callback<TPayload>]` | class, method | Marks typed callback handlers. |
| `[ChatMemberUpdated]` | class, method | Handles `chat_member` updates. |
| `[MyChatMemberUpdated]` | class, method | Handles `my_chat_member` updates. |

## Route Options

`[Command]`, `[CommandTemplate]`, and `[CommandRegex]` default to `/` commands and case-insensitive matching:

```csharp
[Command("start")]
public Task Start(MessageContext ctx, CancellationToken ct) { ... }
```

You can configure prefixes when your bot accepts command-like text from another source:

```csharp
[Command("start", Prefixes = new[] { "/", "!" }, AllowSpaceAfterPrefix = true)]
public Task Start(MessageContext ctx, CancellationToken ct) { ... }
```

Use `PrefixMode` when prefix-less text should be handled as command-shaped input:

```csharp
[CommandTemplate(
    "ticket {id:long}",
    PrefixMode = CommandPrefixMode.Optional)]
public Task Ticket(MessageContext ctx, long id, CancellationToken ct) { ... }
```

Available `CommandPrefixMode` values are:

| Value | Behavior |
| --- | --- |
| `Required` | Requires one of the configured prefixes. This is the default. |
| `Optional` | Accepts a configured prefix or no prefix. |
| `NoPrefix` | Accepts only prefix-less text. Do not combine it with `Prefixes`. |

For prefix-less exact `[Command]` routes, and for `[CommandTemplate]` routes
without placeholders, the whole text must equal the command name. This prevents
short commands such as `[Command("i")]` or `[CommandTemplate("i")]` from matching
a normal sentence like `I need help`. Use `[CommandTemplate]` or
`[CommandRegex]` when prefix-less command-like text has arguments, but keep
those routes reserved for command-shaped words. A prefix-less template based on
a common natural language word plus a string argument will intentionally match
normal sentences that start with that word.

`[Text]` supports explicit match modes:

```csharp
[Text("support", TextMatchMode.Contains, ignoreCase: true)]
public Task Support(MessageContext ctx, CancellationToken ct) { ... }
```

Available `TextMatchMode` values are `Equals`, `StartsWith`, and `Contains`.

Templates are preferred when you need typed route values:

```csharp
[CommandTemplate("ticket {id:long}")]
public Task Ticket(MessageContext ctx, long id, CancellationToken ct) { ... }
```

Use regex only when the input shape cannot be expressed as a template clearly.

## Message Content Filters

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

## Sender And Chat Filters

| Attribute | Purpose |
| --- | --- |
| `[ChatType(...)]` | Limits handler to private, group, supergroup, channel, or sender chats. |
| `[ChatId(...)]` | Limits handler to chat ids. Zero is rejected. |
| `[ChatUsername(...)]` | Limits handler to chat usernames. |
| `[MessageThreadId(...)]` | Limits handler to message thread ids. |
| `[FromUser(...)]` | Limits handler to positive user ids. |
| `[FromBot]` | Filters messages from bot users. |
| `[FromPremiumUser]` | Filters premium users. |
| `[RequireTelegramRole(...)]` | Requires Telegram member statuses. |

## Callback Attributes

| Attribute | Purpose |
| --- | --- |
| `[CallbackData("prefix")]` | Marks a typed callback payload and its compact prefix. |
| `[CallbackDataPrefix("prefix")]` | Filters raw or typed callbacks by callback data prefix. |
| `[HasCallbackData]` | Requires callback data. |
| `[AutoAnswerCallback]` | Auto-answers matching callback handlers. |

## State And Scene Attributes

| Attribute | Purpose |
| --- | --- |
| `[State("state-id")]` | Matches handlers only in a state. |
| `[State<TStateGroup>("Name")]` | Matches a state declared by a state group. |
| `[StateGroup]` | Marks a state group type. |
| `[StateValue("value")]` | Sets custom state segment for a state group property. |
| `[Scene("prefix")]` | Marks a scene and its state prefix. |
| `[SceneStep("StateName")]` | Marks a scene step method. |

## Chat Member Attributes

| Attribute | Purpose |
| --- | --- |
| `[ChatMemberTransition(...)]` | Matches join, leave, promoted, or demoted transitions. |
| `[ChatMemberChanged(old, new)]` | Matches explicit status changes. |

## Error And Module Attributes

| Attribute | Purpose |
| --- | --- |
| `[Error]` | Catch-all error handler. |
| `[Error<TException>]` | Error handler for a specific exception type. |
| `[TelegramModule]` | Marks a module type. |
| `[UseFilter<TFilter>]` | Adds a custom filter. |

## Custom Filter Attribute Base Types

| Type | Purpose |
| --- | --- |
| `TelegramFilterAttribute<TFilter>` | Base class for parameterized custom filter attributes. |
| `ITelegramFilter<TContext>` | Custom filter contract without attribute metadata. |
| `ITelegramFilter<TContext, TAttribute>` | Custom filter contract with attribute metadata. |

## Recommendation

Use the most specific attribute that expresses the route clearly. Prefer templates over regex when possible, and avoid broad catch-all handlers unless they are intentionally last-resort behavior.
