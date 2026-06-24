namespace TeleFlow.Telegram;

public sealed class TelegramRoleFilterOptions
{
    public bool CacheEnabled { get; set; } = true;

    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromSeconds(30);
}
