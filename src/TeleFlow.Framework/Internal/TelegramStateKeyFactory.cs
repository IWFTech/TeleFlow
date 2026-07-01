using TeleFlow.Framework.States;
using TeleFlow.Framework.Updates;
using TeleFlow.Telegram.Internal.Options;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.Telegram.Internal;

internal sealed class TelegramStateKeyFactory : IStateKeyFactory
{
    private readonly string? _botPartitionSegment;

    public TelegramStateKeyFactory(TelegramStateKeyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _botPartitionSegment = options.BotId is { } botId ? $"bot:{botId}" : null;
    }

    public bool TryCreateStateKey(UpdateContext context, out StateKey key)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Payload is not TelegramUpdatePayload payload)
        {
            key = default;
            return false;
        }

        if (TryCreateFromMessage(payload.Update.Message, out key) ||
            TryCreateFromCallback(payload.Update.CallbackQuery, out key))
        {
            return true;
        }

        key = default;
        return false;
    }

    private bool TryCreateFromMessage(Message? message, out StateKey key)
    {
        if (message?.From is null)
        {
            key = default;
            return false;
        }

        key = StateKey.Create(
            scope: "telegram",
            subject: CreateUserSubject(message.From.Id),
            partition: CreateMessagePartition(message));
        return true;
    }

    private bool TryCreateFromCallback(CallbackQuery? callbackQuery, out StateKey key)
    {
        if (callbackQuery is null)
        {
            key = default;
            return false;
        }

        key = StateKey.Create(
            scope: "telegram",
            subject: CreateUserSubject(callbackQuery.From.Id),
            partition: ResolveCallbackPartition(callbackQuery));
        return true;
    }

    private string ResolveCallbackPartition(CallbackQuery callbackQuery)
    {
        if (callbackQuery.Message is not null)
        {
            if (callbackQuery.Message.TryGetMessage(out var message) && message is not null)
            {
                return CreateMessagePartition(message);
            }

            if (callbackQuery.Message.TryGetInaccessibleMessage(out var inaccessibleMessage) &&
                inaccessibleMessage is not null)
            {
                return CreateChatPartition(inaccessibleMessage.Chat.Id);
            }
        }

        return string.IsNullOrWhiteSpace(callbackQuery.ChatInstance)
            ? CreateInlinePartition("unknown")
            : CreateInlinePartition(callbackQuery.ChatInstance);
    }

    private static string CreateUserSubject(long userId)
    {
        return $"user:{userId}";
    }

    private string CreateMessagePartition(Message message)
    {
        var segments = CreatePartitionSegments(capacity: 4);

        if (!string.IsNullOrWhiteSpace(message.BusinessConnectionId))
        {
            segments.Add($"business:{message.BusinessConnectionId}");
        }

        segments.Add($"chat:{message.Chat.Id}");

        if (message.MessageThreadId is not null)
        {
            segments.Add($"thread:{message.MessageThreadId.Value}");
        }

        return string.Join(':', segments);
    }

    private string CreateChatPartition(long chatId)
    {
        var segments = CreatePartitionSegments(capacity: 2);

        segments.Add($"chat:{chatId}");

        return string.Join(':', segments);
    }

    private string CreateInlinePartition(string chatInstance)
    {
        var segments = CreatePartitionSegments(capacity: 2);

        segments.Add($"inline:{chatInstance}");

        return string.Join(':', segments);
    }

    private List<string> CreatePartitionSegments(int capacity)
    {
        var segments = new List<string>(capacity);

        if (_botPartitionSegment is not null)
        {
            segments.Add(_botPartitionSegment);
        }

        return segments;
    }
}
