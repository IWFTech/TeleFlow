using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using TeleFlow.Annotations;
using TeleFlow.Framework.Application;
using TeleFlow.Framework.Dispatching;
using TeleFlow.Framework.Middleware;
using TeleFlow.Framework.Updates;
using TeleFlow.Telegram;
using TeleFlow.Telegram.Formatting;
using TeleFlow.Telegram.I18n;
using TeleFlow.Telegram.I18n.Fluent;
using TeleFlow.Telegram.Schema.Types;

namespace TeleFlow.ArchitectureTests;

public sealed class FluentI18nTests
{
    [Fact]
    public async Task UpdatePipeline_SupportsContextConstructorParameterAndMiddlewareLocalization()
    {
        using var resources = FluentResources.Create(
            ("en", "common.ftl", "probe = localized { $source }"));
        var trace = new List<string>();
        var services = new ServiceCollection();
        services.AddTelegramBot(options => options.Token = "test-token");
        services.AddTelegramFluentI18n(options => options.ResourcesPath = resources.Path);
        services.AddSingleton(trace);
        services.AddUpdateMiddleware<LocalizedProbeMiddleware>();
        services.AddTelegramHandler<ConstructorLocalizedHandler>();
        services.AddTelegramHandler<ParameterLocalizedHandler>();
        services.AddTelegramHandler<ContextLocalizedHandler>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        await ProcessAsync(provider, CreateMessageUpdate("constructor"));
        await ProcessAsync(provider, CreateMessageUpdate("parameter"));
        await ProcessAsync(provider, CreateMessageUpdate("context"));

        Assert.Equal(
        [
            "middleware:localized middleware",
            "handler:localized constructor",
            "middleware:localized middleware",
            "handler:localized parameter",
            "middleware:localized middleware",
            "context:localized plain",
            "context-html:HTML:localized html",
            "context-markdown:MarkdownV2:localized markdown"
        ], trace);
    }

    [Fact]
    public void Formatter_SupportsTermsAttributesReferencesAndPluralSelection()
    {
        using var resources = FluentResources.Create(
            ("en", "common.ftl", """
                -brand = TeleFlow
                inbox = { -brand }: { $count ->
                    [one] One message
                   *[other] { NUMBER($count) } messages
                }
                profile-tabs =
                    .overview = Overview
                    .inventory = Inventory
                inbox-copy = { inbox }
                """));
        using var provider = CreateProvider(resources.Path);
        var formatter = provider.GetRequiredService<IFluentTextFormatter>();

        Assert.Equal("TeleFlow: 2 messages", formatter.Format(new Locale("en"), "inbox", ("count", 2)));
        Assert.Equal("Inventory", formatter.Format(new Locale("en"), "profile-tabs.inventory"));
        Assert.Equal("TeleFlow: One message", formatter.Format(new Locale("en"), "inbox-copy", ("count", 1)));
    }

    [Fact]
    public void Formatter_UsesExactParentAndConfiguredFallbackCatalogs()
    {
        using var resources = FluentResources.Create(
            ("en", "common.ftl", "title = English"),
            ("ru", "common.ftl", "title = Russian"),
            ("ru-RU", "common.ftl", "title = Russian Russia"));
        using var provider = CreateProvider(resources.Path);
        var formatter = provider.GetRequiredService<IFluentTextFormatter>();

        Assert.Equal("Russian Russia", formatter.Format(new Locale("ru-RU"), "title"));
        Assert.Equal("Russian", formatter.Format(new Locale("ru-UA"), "title"));
        Assert.Equal("English", formatter.Format(new Locale("de-DE"), "title"));
    }

    [Fact]
    public void Formatter_EscapesDynamicHtmlAndPreservesStaticMarkupAndMatchingFragments()
    {
        using var resources = FluentResources.Create(
            ("en", "common.ftl", "message = <b>Hello, { $name }</b> { $emoji }"));
        using var provider = CreateProvider(resources.Path);
        var formatter = provider.GetRequiredService<IFluentTextFormatter>();
        var emoji = TelegramHtml.Create().CustomEmoji("123", "💎").Build();

        var result = formatter.FormatHtml(
            new Locale("en"),
            "message",
            ("name", "<admin> & owner"),
            ("emoji", emoji));

        Assert.Equal(TelegramParseMode.Html, result.ParseMode);
        Assert.Equal(
            "<b>Hello, &lt;admin&gt; &amp; owner</b> <tg-emoji emoji-id=\"123\">💎</tg-emoji>",
            result.Text);
    }

    [Fact]
    public void Formatter_PreventsDynamicHtmlFromLeavingQuotedAttributes()
    {
        using var resources = FluentResources.Create(
            ("en", "common.ftl", "link = <a href=\"https://example.com?q={ $query }\">Open</a>"));
        using var provider = CreateProvider(resources.Path);
        var formatter = provider.GetRequiredService<IFluentTextFormatter>();

        var result = formatter.FormatHtml(
            new Locale("en"),
            "link",
            ("query", "value\" onclick=\"unexpected'"));

        Assert.Equal(
            "<a href=\"https://example.com?q=value&quot; onclick=&quot;unexpected&#39;\">Open</a>",
            result.Text);
    }

    [Fact]
    public void Formatter_EscapesDynamicMarkdownV2AndPreservesStaticMarkup()
    {
        using var resources = FluentResources.Create(
            ("en", "common.ftl", "message = *Hello, { $name }*"));
        using var provider = CreateProvider(resources.Path);
        var formatter = provider.GetRequiredService<IFluentTextFormatter>();

        var result = formatter.FormatMarkdownV2(
            new Locale("en"),
            "message",
            ("name", "user.name-test"));

        Assert.Equal(TelegramParseMode.MarkdownV2, result.ParseMode);
        Assert.Equal("*Hello, user\\.name\\-test*", result.Text);
    }

    [Fact]
    public void Formatter_RejectsFormattedTextWithMismatchedParseMode()
    {
        using var resources = FluentResources.Create(
            ("en", "common.ftl", "message = { $fragment }"));
        using var provider = CreateProvider(resources.Path);
        var formatter = provider.GetRequiredService<IFluentTextFormatter>();
        var markdown = TelegramMarkdownV2.Create().Bold("unsafe for HTML").Build();

        var exception = Assert.Throws<FluentLocalizationException>(
            () => formatter.FormatHtml(new Locale("en"), "message", ("fragment", markdown)));

        Assert.Equal("message", exception.MessageId);
        Assert.Equal(new Locale("en"), exception.Locale);
        Assert.DoesNotContain(markdown.Text, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Formatter_SupportsLocaleAwareNumberAndDateTimeFunctions()
    {
        using var resources = FluentResources.Create(
            ("en-US", "common.ftl", """
                amount = { NUMBER($value, minimumFractionDigits: 2, maximumFractionDigits: 2) }
                ungrouped = { NUMBER($value, maximumFractionDigits: 0, useGrouping: "false") }
                timestamp = { DATETIME($value, dateStyle: "short", timeStyle: "short") }
                """));
        using var provider = CreateProvider(resources.Path, fallbackLocale: "en-US");
        var formatter = provider.GetRequiredService<IFluentTextFormatter>();
        var locale = new Locale("en-US");

        Assert.Equal("1,250.50", formatter.Format(locale, "amount", ("value", 1_250.5)));
        Assert.Equal("1250", formatter.Format(locale, "ungrouped", ("value", 1_250)));

        var timestamp = formatter.Format(
            locale,
            "timestamp",
            ("value", new DateTimeOffset(2026, 7, 22, 13, 45, 0, TimeSpan.Zero)));

        Assert.Contains("7/22/2026", timestamp, StringComparison.Ordinal);
        Assert.Contains("1:45", timestamp, StringComparison.Ordinal);
    }

    [Fact]
    public void Formatter_SupportsDateOnlyAndTimeOnlyWithoutExplicitStyles()
    {
        using var resources = FluentResources.Create(
            ("en-US", "common.ftl", "date = { DATETIME($value) }\ntime = { DATETIME($value) }"));
        using var provider = CreateProvider(resources.Path, fallbackLocale: "en-US");
        var formatter = provider.GetRequiredService<IFluentTextFormatter>();
        var locale = new Locale("en-US");
        var date = new DateOnly(2026, 7, 22);
        var time = new TimeOnly(13, 45);

        Assert.Equal(date.ToString("d", locale.Culture), formatter.Format(locale, "date", ("value", date)));
        Assert.Equal(time.ToString("t", locale.Culture), formatter.Format(locale, "time", ("value", time)));
    }

    [Theory]
    [InlineData("short", "short", "g")]
    [InlineData("short", "long", "G")]
    [InlineData("long", "short", "f")]
    [InlineData("long", "long", "F")]
    public void Formatter_MapsDateTimeStylePairsToDotNetCulturePatterns(
        string dateStyle,
        string timeStyle,
        string expectedFormat)
    {
        using var resources = FluentResources.Create(
            ("en-US", "common.ftl", $"timestamp = {{ DATETIME($value, dateStyle: \"{dateStyle}\", timeStyle: \"{timeStyle}\") }}"));
        using var provider = CreateProvider(resources.Path, fallbackLocale: "en-US");
        var formatter = provider.GetRequiredService<IFluentTextFormatter>();
        var locale = new Locale("en-US");
        var timestamp = new DateTimeOffset(2026, 7, 22, 13, 45, 30, TimeSpan.Zero);

        Assert.Equal(
            timestamp.ToString(expectedFormat, locale.Culture),
            formatter.Format(locale, "timestamp", ("value", timestamp)));
    }

    [Theory]
    [InlineData("medium")]
    [InlineData("full")]
    public void Formatter_RejectsDateTimeStylesWithoutHonestDotNetSemantics(string style)
    {
        using var resources = FluentResources.Create(
            ("en-US", "common.ftl", $"timestamp = {{ DATETIME($value, dateStyle: \"{style}\") }}"));
        using var provider = CreateProvider(resources.Path, fallbackLocale: "en-US");
        var formatter = provider.GetRequiredService<IFluentTextFormatter>();

        var exception = Assert.Throws<FluentLocalizationException>(
            () => formatter.Format(
                new Locale("en-US"),
                "timestamp",
                ("value", new DateOnly(2026, 7, 22))));

        Assert.Contains("argument or Fluent function", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Formatter_SupportsRichHtmlWithEscapedLatexArgument()
    {
        using var resources = FluentResources.Create(
            ("en", "common.ftl", "formula = <tg-math-block>{ $formula }</tg-math-block>"));
        using var provider = CreateProvider(resources.Path);
        var formatter = provider.GetRequiredService<IFluentTextFormatter>();

        var result = formatter.FormatHtml(
            new Locale("en"),
            "formula",
            ("formula", @"P(A < B) = \frac{1}{2}"));

        Assert.Equal("<tg-math-block>P(A &lt; B) = \\frac{1}{2}</tg-math-block>", result.Text);
    }

    [Fact]
    public void Formatter_ThrowsDedicatedExceptionForMissingKeyAttributeOrVariable()
    {
        using var resources = FluentResources.Create(
            ("en", "common.ftl", "greeting = Hello, { $name }"));
        using var provider = CreateProvider(resources.Path);
        var formatter = provider.GetRequiredService<IFluentTextFormatter>();
        var locale = new Locale("en");

        AssertLocalizationFailure(() => formatter.Format(locale, "missing"), "missing", locale);
        AssertLocalizationFailure(() => formatter.Format(locale, "greeting.missing"), "greeting.missing", locale);
        AssertLocalizationFailure(() => formatter.Format(locale, "greeting"), "greeting", locale);
    }

    [Fact]
    public void RuntimeValidation_RejectsMissingFallbackCatalogAndInvalidResources()
    {
        using var missingFallback = FluentResources.Create(("ru", "common.ftl", "title = Заголовок"));
        using var missingFallbackProvider = CreateProvider(
            missingFallback.Path,
            fallbackLocale: "en",
            validate: false);

        var missingException = Assert.Throws<TeleFlowConfigurationException>(
            () => ValidateRuntime(missingFallbackProvider));
        Assert.Contains("fallback", missingException.Message, StringComparison.OrdinalIgnoreCase);

        using var invalid = FluentResources.Create(("en", "broken.ftl", "broken = {"));
        using var invalidProvider = CreateProvider(invalid.Path, validate: false);

        var invalidException = Assert.Throws<TeleFlowConfigurationException>(
            () => ValidateRuntime(invalidProvider));
        Assert.Contains("broken.ftl", invalidException.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Formatter_DoesNotReadResourceFilesAfterStartupValidation()
    {
        using var resources = FluentResources.Create(("en", "common.ftl", "title = Loaded once"));
        using var provider = CreateProvider(resources.Path);
        var formatter = provider.GetRequiredService<IFluentTextFormatter>();

        resources.DeleteFiles();

        Assert.Equal("Loaded once", formatter.Format(new Locale("en"), "title"));
    }

    [Fact]
    public async Task Formatter_IsolatesConcurrentLocales()
    {
        using var resources = FluentResources.Create(
            ("en", "common.ftl", "title = English { $value }"),
            ("ru", "common.ftl", "title = Русский { $value }"));
        using var provider = CreateProvider(resources.Path);
        var formatter = provider.GetRequiredService<IFluentTextFormatter>();

        var tasks = Enumerable.Range(0, 200)
            .Select(index => Task.Run(() => index % 2 == 0
                ? formatter.Format(new Locale("en"), "title", ("value", index))
                : formatter.Format(new Locale("ru"), "title", ("value", index))))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.All(results.Where((_, index) => index % 2 == 0), static result => Assert.StartsWith("English", result));
        Assert.All(results.Where((_, index) => index % 2 != 0), static result => Assert.StartsWith("Русский", result));
    }

    [Fact]
    public void Formatter_ZeroArgumentPathAllocatesLessThanArgumentFormatting()
    {
        using var resources = FluentResources.Create(
            ("en", "common.ftl", "ready = Ready\nvalue = Value: { $value }"));
        using var provider = CreateProvider(resources.Path);
        var formatter = provider.GetRequiredService<IFluentTextFormatter>();
        var locale = new Locale("en");

        var withoutArguments = MeasureAllocations(
            () => formatter.Format(locale, "ready"));
        var withArguments = MeasureAllocations(
            () => formatter.Format(locale, "value", ("value", 42)));

        Assert.True(
            withArguments > withoutArguments,
            $"Expected argument formatting ({withArguments} bytes) to allocate more than the zero-argument path ({withoutArguments} bytes).");
    }

    private static ServiceProvider CreateProvider(
        string resourcesPath,
        string fallbackLocale = "en",
        bool validate = true)
    {
        var services = new ServiceCollection();
        services.AddTelegramBot(options => options.Token = "test-token");
        services.AddTelegramFluentI18n(options =>
        {
            options.ResourcesPath = resourcesPath;
            options.FallbackLocale = new Locale(fallbackLocale);
        });

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        if (validate)
        {
            ValidateRuntime(provider);
        }

        return provider;
    }

    private static Task ProcessAsync(ServiceProvider provider, Update update)
    {
        var processor = new DefaultUpdateProcessor(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IUpdateDispatcher>(),
            provider.GetServices<UpdateMiddlewareRegistration>());

        return processor.ProcessAsync(new TelegramUpdatePayload(update));
    }

    private static Update CreateMessageUpdate(string text)
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
                    LanguageCode = "en"
                },
                Chat = new Chat { Id = 100, Type = "private" },
                Text = text
            }
        };
    }

    private static void ValidateRuntime(IServiceProvider provider)
    {
        foreach (var validator in provider.GetServices<ITeleFlowRuntimeValidator>())
        {
            validator.Validate(provider);
        }
    }

    private static void AssertLocalizationFailure(Action action, string messageId, Locale locale)
    {
        var exception = Assert.Throws<FluentLocalizationException>(action);
        Assert.Equal(messageId, exception.MessageId);
        Assert.Equal(locale, exception.Locale);
        Assert.DoesNotContain("Hello", exception.Message, StringComparison.Ordinal);
    }

    private static long MeasureAllocations(Func<string> format)
    {
        const int WarmupCount = 32;
        const int MeasurementCount = 256;

        for (var index = 0; index < WarmupCount; index++)
        {
            _ = format();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        string? lastResult = null;

        for (var index = 0; index < MeasurementCount; index++)
        {
            lastResult = format();
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(lastResult);
        return allocated;
    }

    private sealed class FluentResources : IDisposable
    {
        private FluentResources(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static FluentResources Create(params (string Locale, string FileName, string Content)[] files)
        {
            var root = Directory.CreateTempSubdirectory("teleflow-i18n-");

            foreach (var (locale, fileName, content) in files)
            {
                var localeDirectory = Directory.CreateDirectory(System.IO.Path.Combine(root.FullName, locale));
                System.IO.File.WriteAllText(System.IO.Path.Combine(localeDirectory.FullName, fileName), content);
            }

            return new FluentResources(root.FullName);
        }

        public void DeleteFiles()
        {
            foreach (var file in Directory.EnumerateFiles(Path, "*.ftl", SearchOption.AllDirectories))
            {
                System.IO.File.Delete(file);
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    private sealed class LocalizedProbeMiddleware(
        IFluentLocalizer localizer,
        List<string> trace) : IUpdateMiddleware
    {
        public async Task InvokeAsync(UpdateContext context, UpdateDelegate next)
        {
            trace.Add("middleware:" + localizer.Format("probe", ("source", "middleware")));
            await next(context);
        }
    }

    private sealed class ConstructorLocalizedHandler(
        IFluentLocalizer localizer,
        List<string> trace)
    {
        [Text("constructor")]
        public Task Handle(MessageContext context)
        {
            trace.Add("handler:" + localizer.Format("probe", ("source", "constructor")));
            return Task.CompletedTask;
        }
    }

    private sealed class ParameterLocalizedHandler(List<string> trace)
    {
        [Text("parameter")]
        public Task Handle(MessageContext context, IFluentLocalizer localizer)
        {
            trace.Add("handler:" + context.I18n("probe", ("source", "parameter")));
            _ = localizer;
            return Task.CompletedTask;
        }
    }

    private sealed class ContextLocalizedHandler(List<string> trace)
    {
        [Text("context")]
        public Task Handle(MessageContext context)
        {
            var plain = context.I18n("probe", ("source", "plain"));
            var html = context.I18nHtml("probe", ("source", "html"));
            var markdown = context.I18nMarkdownV2("probe", ("source", "markdown"));

            trace.Add("context:" + plain);
            trace.Add($"context-html:{html.ParseMode}:{html.Text}");
            trace.Add($"context-markdown:{markdown.ParseMode}:{markdown.Text}");
            return Task.CompletedTask;
        }
    }
}
