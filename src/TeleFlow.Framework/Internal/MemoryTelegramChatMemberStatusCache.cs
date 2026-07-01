using System.Collections.Concurrent;
using TeleFlow.Annotations;

namespace TeleFlow.Telegram.Internal;

internal sealed class MemoryTelegramChatMemberStatusCache : ITelegramChatMemberStatusCache
{
    private readonly ConcurrentDictionary<CacheKey, CacheEntry> _entries = [];
    private readonly TimeProvider _timeProvider;

    public MemoryTelegramChatMemberStatusCache(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
    }

    public ValueTask<TelegramMemberStatusSet?> GetAsync(
        long chatId,
        long userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var key = new CacheKey(chatId, userId);

        if (!_entries.TryGetValue(key, out var entry))
        {
            return ValueTask.FromResult<TelegramMemberStatusSet?>(null);
        }

        if (entry.ExpiresAt <= _timeProvider.GetUtcNow())
        {
            _entries.TryRemove(key, out _);
            return ValueTask.FromResult<TelegramMemberStatusSet?>(null);
        }

        return ValueTask.FromResult<TelegramMemberStatusSet?>(entry.Status);
    }

    public ValueTask SetAsync(
        long chatId,
        long userId,
        TelegramMemberStatusSet status,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!TelegramMemberStatusSetValidator.IsValid(status))
        {
            throw new ArgumentException("Telegram member status cache value must contain a known status.", nameof(status));
        }

        if (ttl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ttl), "Telegram member status cache TTL must be greater than zero.");
        }

        var key = new CacheKey(chatId, userId);
        var entry = new CacheEntry(status, _timeProvider.GetUtcNow().Add(ttl));
        _entries[key] = entry;
        return ValueTask.CompletedTask;
    }

    private readonly record struct CacheKey(long ChatId, long UserId);

    private readonly record struct CacheEntry(TelegramMemberStatusSet Status, DateTimeOffset ExpiresAt);
}
