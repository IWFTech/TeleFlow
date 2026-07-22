using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Framework.Dispatching;
using TeleFlow.Framework.Middleware;
using TeleFlow.Framework.Updates;
using TeleFlow.Telegram;
using TeleFlow.Telegram.I18n;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.ArchitectureTests;

public sealed class TelegramI18nTests
{
    [Fact]
    public void Locale_NormalizesCultureNamesAndComparesOrdinallyIgnoringCase()
    {
        var first = new Locale("RU-ru");
        var second = new Locale("ru-RU");

        Assert.Equal("ru-RU", first.Name);
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Equal("ru-RU", first.ToString());
    }

    [Fact]
    public void Locale_RejectsInvalidCultureNames()
    {
        Assert.Throws<ArgumentException>(() => new Locale("not_a_locale"));
    }

    [Fact]
    public void Locale_TryCreateReturnsNullOnlyForUnusableInput()
    {
        Assert.True(Locale.TryCreate("RU-ru", out var valid));
        Assert.Equal("ru-RU", valid.Name);

        Assert.False(Locale.TryCreate("not_a_locale", out var invalid));
        Assert.Null(invalid);
    }

    [Fact]
    public void LocaleAccessor_FailsClearlyOutsideResolvedUpdateScope()
    {
        var services = CreateServices();

        using var provider = BuildProvider(services);
        using var scope = provider.CreateScope();
        var accessor = scope.ServiceProvider.GetRequiredService<ILocaleAccessor>();

        Assert.False(accessor.IsAvailable);
        var exception = Assert.Throws<InvalidOperationException>(() => accessor.Current);
        Assert.Contains("locale", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("update", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LocaleMiddleware_UsesFirstCustomResolverDecisionExactlyOnce()
    {
        var trace = new LocaleTrace();
        var services = CreateServices();
        services.AddSingleton(trace);
        services.AddTelegramLocaleResolver<NoDecisionLocaleResolver>();
        services.AddTelegramLocaleResolver<PersistedLocaleResolver>();
        services.AddSingleton<IUpdateDispatcher, LocaleRecordingDispatcher>();

        using var provider = BuildProvider(services);
        await ProcessAsync(provider, CreateMessageUpdate("de-DE"));

        Assert.Equal(["none", "persisted", "dispatch:uk-UA"], trace.Events);
    }

    [Fact]
    public async Task LocaleMiddleware_UsesTelegramLanguageCodeBeforeFallback()
    {
        var trace = new LocaleTrace();
        var services = CreateServices();
        services.AddSingleton(trace);
        services.AddSingleton<IUpdateDispatcher, LocaleRecordingDispatcher>();

        using var provider = BuildProvider(services);
        await ProcessAsync(provider, CreateMessageUpdate("ru-RU"));

        Assert.Equal(["dispatch:ru-RU"], trace.Events);
    }

    [Fact]
    public async Task LocaleMiddleware_UsesTelegramLanguageCodeForNonMessageUpdates()
    {
        var trace = new LocaleTrace();
        var services = CreateServices();
        services.AddSingleton(trace);
        services.AddSingleton<IUpdateDispatcher, LocaleRecordingDispatcher>();

        using var provider = BuildProvider(services);
        await ProcessAsync(provider, CreateInlineQueryUpdate("uk-UA"));

        Assert.Equal(["dispatch:uk-UA"], trace.Events);
    }

    [Fact]
    public async Task LocaleMiddleware_UsesFallbackForMissingOrInvalidTelegramLanguageCode()
    {
        var trace = new LocaleTrace();
        var services = CreateServices();
        services.AddSingleton(trace);
        services.AddSingleton<IUpdateDispatcher, LocaleRecordingDispatcher>();

        using var provider = BuildProvider(services);
        await ProcessAsync(provider, CreateMessageUpdate(null));
        await ProcessAsync(provider, CreateMessageUpdate("not_a_locale"));

        Assert.Equal(["dispatch:en", "dispatch:en"], trace.Events);
    }

    [Fact]
    public async Task LocaleMiddleware_PropagatesResolverFailures()
    {
        var services = CreateServices();
        services.AddTelegramLocaleResolver<ThrowingLocaleResolver>();
        services.AddSingleton<IUpdateDispatcher, LocaleRecordingDispatcher>();

        using var provider = BuildProvider(services);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => ProcessAsync(provider, CreateMessageUpdate("ru")));

        Assert.Equal("locale storage failed", exception.Message);
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddTelegramBot(options => options.Token = "test-token");
        services.AddTelegramI18n(options => options.FallbackLocale = new Locale("en"));
        return services;
    }

    private static ServiceProvider BuildProvider(IServiceCollection services)
    {
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    private static Task ProcessAsync(ServiceProvider provider, Update update)
    {
        var processor = new DefaultUpdateProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IUpdateDispatcher>(),
            provider.GetServices<UpdateMiddlewareRegistration>());

        return processor.ProcessAsync(new TelegramUpdatePayload(update));
    }

    private static Update CreateMessageUpdate(string? languageCode)
    {
        return new Update
        {
            UpdateId = 1,
            Message = new Message
            {
                MessageId = 10,
                Date = 0,
                From = new User
                {
                    Id = 5,
                    IsBot = false,
                    FirstName = "User",
                    LanguageCode = languageCode
                },
                Chat = new Chat { Id = 100, Type = "private" },
                Text = "/start"
            }
        };
    }

    private static Update CreateInlineQueryUpdate(string languageCode)
    {
        return new Update
        {
            UpdateId = 2,
            InlineQuery = new InlineQuery
            {
                Id = "query",
                From = new User
                {
                    Id = 6,
                    IsBot = false,
                    FirstName = "Inline user",
                    LanguageCode = languageCode
                },
                Query = "search",
                Offset = string.Empty
            }
        };
    }

    private sealed class LocaleTrace
    {
        public List<string> Events { get; } = [];
    }

    private sealed class NoDecisionLocaleResolver(LocaleTrace trace) : ILocaleResolver
    {
        public ValueTask<Locale?> TryResolveAsync(
            LocaleResolutionContext context,
            CancellationToken cancellationToken = default)
        {
            trace.Events.Add("none");
            return ValueTask.FromResult<Locale?>(null);
        }
    }

    private sealed class PersistedLocaleResolver(LocaleTrace trace) : ILocaleResolver
    {
        public ValueTask<Locale?> TryResolveAsync(
            LocaleResolutionContext context,
            CancellationToken cancellationToken = default)
        {
            trace.Events.Add("persisted");
            return ValueTask.FromResult<Locale?>(new Locale("uk-UA"));
        }
    }

    private sealed class ThrowingLocaleResolver : ILocaleResolver
    {
        public ValueTask<Locale?> TryResolveAsync(
            LocaleResolutionContext context,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("locale storage failed");
        }
    }

    private sealed class LocaleRecordingDispatcher(LocaleTrace? trace = null) : IUpdateDispatcher
    {
        public Task DispatchAsync(UpdateContext context, CancellationToken cancellationToken = default)
        {
            var locale = context.Services.GetRequiredService<ILocaleAccessor>();
            trace?.Events.Add($"dispatch:{locale.Current.Name}");
            return Task.CompletedTask;
        }
    }
}
