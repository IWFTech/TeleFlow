# Routing Contract

This page defines the observable routing rules of TeleFlow. It complements the
[handlers guide](../fundamentals/handlers-and-routing.md).

## Selection Order

For a message update, TeleFlow tries command routes before message routes. A
message that matches no command can still reach a `[Message]`, `[Text]`,
template, or regex handler.

Within each route family, selection is deterministic:

| Rule | Earlier candidate wins |
| --- | --- |
| State | A handler for the current state before a stateless handler |
| Command route kind | Exact command, then command template, then command regex |
| Message route kind | Exact text, then text template, then text regex, then a bare message handler |
| Route specificity | The more specific route of the same kind |
| Final tie | Registration order |

Filters run after the route shape matches. A filter rejection continues search
for another candidate; it is not an error. Typed callback routes run before raw
`[Callback]` fallbacks. Chat-member routes require both update kind and declared
transition to match before filters run.

## Command Prefixes

`CommandPrefixMode.Required` accepts configured prefixes only.
`CommandPrefixMode.Optional` tries a configured prefix and then prefix-less
command text. `CommandPrefixMode.NoPrefix` accepts prefix-less text only.

An exact prefix-less command must consume the whole message. Thus
`[Command("help", PrefixMode = CommandPrefixMode.Optional)]` matches `help`,
but not `help please`. A prefixed exact command may have arguments, so
`/help please` still reaches the `help` handler. `AllowSpaceAfterPrefix`
controls whitespace after a configured prefix.

### Overlapping Prefixes

The longest matching prefix wins, regardless of declaration order:

```csharp
[Command("confirm", Prefixes = new[] { "!", "!!" })]
public Task Confirm(MessageContext ctx, CancellationToken ct)
{
    return ctx.Message.AnswerAsync("Confirmed.", ct);
}
```

`!!confirm` uses `!!`, even when `!` is declared first. Prefixes are normalized
once while route metadata is built, not for every update.

## Slash Command Mentions

Telegram group commands can include a bot mention, for example
`/start@my_bot`. TeleFlow accepts it only when `my_bot` is the current bot.
`/start@another_bot` is never treated as `/start`.

Configure `BotUsername` when the username is known locally:

```csharp
builder.Services.AddTelegramBot(options =>
{
    options.Token = token;
    options.BotUsername = "my_bot";
});
```

For standard `AddLongPolling()` and `AddWebhook()` applications, TeleFlow calls
`getMe` once before the transport starts when `BotUsername` is absent. The
username is cached for the process lifetime. Failure prevents the hosted
transport from becoming ready. Route selection never performs Telegram I/O.

For a custom/direct update pipeline without a TeleFlow transport, configure
`BotUsername` when mention-qualified commands must work. A bare `/start` never
needs bot identity.

## Templates, Regexes, And Text

`[TextTemplate]` and `[CommandTemplate]` are anchored by TeleFlow: the entire
relevant text or command body must fit the template. Invalid optional or typed
values simply make the route not match, allowing the next candidate.

`[TextRegex]` and `[CommandRegex]` preserve the application's expression. Add
`^` and `$` when it must cover the whole input.

Exact command comparisons and command-template matching normalize the configured
pattern and incoming command body to Unicode NFC before applying the route's
ordinal, culture-independent `IgnoreCase` setting. This lets canonically
equivalent sequences such as composed `ё` and `е` plus a combining diaeresis
match the same command. String values bound by `[CommandTemplate]` are NFC.

The original message text exposed through `MessageContext` is never changed.
TeleFlow does not treat the distinct letters `е` and `ё` as aliases. Declare
both routes explicitly when both spellings should be accepted.

Text routes and regex routes preserve the application's input contract and do
not apply implicit Unicode normalization. Normalize explicitly in a regex when
that route needs canonical-equivalence behavior.

## Registration Parity

Generated registration through `AddTelegramHandlersFromAssembly(...)` and
explicit `AddTelegramHandler<T>()` use the same runtime route table and
selector. Generated registration is the recommended application default;
explicit registration remains useful for focused modules and tests.
