namespace TeleFlow.Annotations;
/// <summary>
/// Restricts a handler or handler class to Telegram bots, optionally limited by bot user id.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class FromBotAttribute : TeleFlowAttribute
{
    /// <summary>
    /// Creates a sender bot filter.
    /// </summary>
    /// <param name="botIds">Optional Telegram bot user ids allowed to match.</param>
    public FromBotAttribute(params long[] botIds)
    {
        ArgumentNullException.ThrowIfNull(botIds);

        if (botIds.Any(static botId => botId <= 0))
        {
            throw new ArgumentException("Telegram bot ids must be positive.", nameof(botIds));
        }

        BotIds = botIds.ToArray();
    }

    /// <summary>
    /// Optional Telegram bot user ids allowed to match. An empty collection allows any bot.
    /// </summary>
    public IReadOnlyList<long> BotIds { get; }
}
