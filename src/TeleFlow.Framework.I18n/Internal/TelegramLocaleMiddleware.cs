using TeleFlow.Framework.Middleware;
using TeleFlow.Framework.Updates;

namespace TeleFlow.Telegram.I18n.Internal;

/// <summary>
/// Resolves one locale per Telegram update before handler dispatch, then exposes it synchronously through the scoped accessor.
/// </summary>
internal sealed class TelegramLocaleMiddleware(
    IEnumerable<ILocaleResolver> resolvers,
    ITelegramCurrentUpdateAccessor currentUpdate,
    LocaleAccessor localeAccessor,
    TelegramI18nOptions options) : IUpdateMiddleware
{
    private readonly IReadOnlyList<ILocaleResolver> _resolvers = resolvers.ToArray();

    public async Task InvokeAsync(UpdateContext context, UpdateDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var resolutionContext = new LocaleResolutionContext(
            currentUpdate.Update,
            currentUpdate.User,
            currentUpdate.Chat);
        var locale = await ResolveAsync(resolutionContext, context.CancellationToken).ConfigureAwait(false);

        localeAccessor.Initialize(locale);
        await next(context).ConfigureAwait(false);
    }

    private async ValueTask<Locale> ResolveAsync(
        LocaleResolutionContext context,
        CancellationToken cancellationToken)
    {
        foreach (var resolver in _resolvers)
        {
            var locale = await resolver
                .TryResolveAsync(context, cancellationToken)
                .ConfigureAwait(false);

            if (locale is not null)
            {
                return locale;
            }
        }

        return Locale.TryCreate(context.User?.LanguageCode, out var telegramLocale)
            ? telegramLocale
            : options.FallbackLocale;
    }
}
