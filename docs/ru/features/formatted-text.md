# Форматированный текст и custom emoji

Используй builders форматированного текста для обычных Telegram-сообщений с
именами, ссылками, балансами, spoiler, кодом или custom emoji. Builders
экранируют обычные значения и всегда задают явный Bot API parse mode.

```csharp
using TeleFlow.Telegram.Formatting;

var text = TelegramHtml.Create()
    .Text("Баланс: ")
    .Bold(balance.ToString("N0") + " BB")
    .LineBreak()
    .Link(profileUrl, "Открыть профиль")
    .Text(" ")
    .CustomEmoji(
        customEmojiId: "5368324170671202286",
        fallbackEmoji: "💎")
    .Build();

await ctx.Message.AnswerAsync(text, ct);
```

Результат - `TelegramFormattedText`. В нём есть готовый `Text` и явный
`TelegramParseMode`, поэтому он не зависит от `TelegramBotDefaults.ParseMode`.

## Формат выбирается явно

```csharp
var html = TelegramHtml.Create()
    .Text("Статус: ")
    .Bold("активен")
    .Build();

var markdown = TelegramMarkdownV2.Create()
    .Text("Статус: ")
    .Bold("активен")
    .Build();
```

HTML и MarkdownV2 - разные форматы. TeleFlow не угадывает формат по строке и
не конвертирует один формат в другой. HTML-результат нельзя добавить в
MarkdownV2 builder, и наоборот.

## Безопасный текст и композиция

`Text(...)` и все обычные текстовые аргументы экранируют входные данные. Имя
пользователя, значение из БД или внешний текст передавай напрямую:

```csharp
var text = TelegramHtml.Create()
    .Text(player.DisplayName)
    .Text(" вошёл в чат.")
    .Build();
```

Не экранируй значение вручную заранее. Builder сохраняет форматированные
фрагменты при композиции, поэтому вложенный контент экранируется ровно один раз:

```csharp
var text = TelegramHtml.Create()
    .Bold(content => content
        .Text("Активен: ")
        .Spoiler(secretValue))
    .Build();
```

Безопасный vocabulary: `Text`, `LineBreak`, `Bold`, `Italic`, `Underline`,
`Strikethrough`, `Spoiler`, `Code`, `Pre`, `Link`, `Mention`, `BlockQuote` и
`CustomEmoji`.

В `Code`, `Pre`, ссылках, mentions, custom emoji и block quote передаётся
обычный текст, а не вложенный builder. Telegram разрешает вложенные entities не
во всех этих конструкциях, поэтому API не создаёт ложную свободу.

```csharp
var text = TelegramMarkdownV2.Create()
    .BlockQuote("Здесь важное примечание.", expandable: true)
    .LineBreak()
    .Code("dotnet test")
    .LineBreak()
    .Pre("Console.WriteLine(\"Hello\");", language: "csharp")
    .Build();
```

## Ссылки, mentions и custom emoji

```csharp
var text = TelegramHtml.Create()
    .Link(new Uri("https://example.com/profile/42"), "Профиль")
    .Text(" пользователя ")
    .Mention(123456789, "Алиса")
    .LineBreak()
    .CustomEmoji(customEmojiId, fallbackEmoji: "💎")
    .Build();
```

Builder требует absolute URI и положительный user id. Для custom emoji нужен
ещё и fallback emoji. Telegram может показать его в notification, forwarded
message, старом клиенте или там, где bot/account/destination не имеет права
отрисовать custom emoji. TeleFlow не вызывает `getCustomEmojiStickers`
автоматически и не обещает доступность emoji.

Custom emoji внутри **inline keyboard button** - отдельное поле Bot API:

```csharp
var keyboard = InlineKeyboardBuilder.Create()
    .Button(
        "Открыть",
        new OpenProfileCallback(),
        new InlineKeyboardButtonOptions
        {
            IconCustomEmojiId = customEmojiId
        })
    .Build();
```

## Отправка и редактирование

Framework context helpers принимают `TelegramFormattedText`:

```csharp
await ctx.Message.AnswerAsync(text, ct);
await ctx.Message.ReplyAsync(text, ct);
await ctx.Callback.EditTextAsync(text, ct);

await ctx.Message.AnswerAsync(text, keyboard, ct);
await ctx.Callback.EditTextAsync(text, keyboard, ct);
```

Formatted overloads передают явный parse mode через обычный generated-client
path. Старые `string` overloads не меняются и продолжают учитывать client
default parse mode.

Для reply keyboards, force replies, captions, явных `MessageEntity` или другого
сложного поля Bot API используй generated client напрямую:

```csharp
await ctx.Bot.SendMessageAsync(
    chatId: IntegerString.From(ctx.TelegramChat.Id),
    text: text.Text,
    parseMode: text.ParseMode,
    cancellationToken: ct);
```

## Unsafe markup

`UnsafeMarkup` существует только для статичных проверенных шаблонов:

```csharp
var text = TelegramHtml.UnsafeMarkup(
    "<b>Проверенный статичный текст</b>");
```

Он отключает экранирование и structural validation. Никогда не передавай в него
данные пользователя, значения из БД или внешний текст.

## Entities и Rich Messages

Builders работают с обычным Bot API `text` и `parse_mode`. Они не заменяют
явные `MessageEntity` и не строят Bot API **Rich Messages**
(`InputRichMessage`, blocks, media, drafts, tables, formulas). Это отдельная
модель Telegram API, и она намеренно не смешана с этим helper-слоем.
