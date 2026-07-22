using System.Collections.Frozen;
using System.Globalization;
using Linguini.Bundle;
using Linguini.Bundle.Builder;
using Linguini.Bundle.Errors;
using TeleFlow.Framework.Application;

namespace TeleFlow.Telegram.I18n.Fluent.Internal;

/// <summary>
/// Owns the immutable in-memory Fluent catalog loaded from application resources during runtime validation.
/// Formatting performs exact, parent, and fallback selection without file access or resource parsing.
/// </summary>
internal sealed class FluentCatalog(TelegramFluentI18nOptions options)
{
    private readonly object _initializationLock = new();
    private FrozenDictionary<string, LocaleBundles>? _bundles;

    public void Initialize()
    {
        if (_bundles is not null)
        {
            return;
        }

        lock (_initializationLock)
        {
            if (_bundles is not null)
            {
                return;
            }

            _bundles = LoadCatalog();
        }
    }

    public ResolvedFluentBundle Resolve(Locale requestedLocale, FluentRenderingMode mode)
    {
        ArgumentNullException.ThrowIfNull(requestedLocale);

        var bundles = _bundles ?? throw new InvalidOperationException(
            "The Fluent catalog has not been initialized. Run TeleFlow runtime validation before formatting messages.");

        for (var culture = requestedLocale.Culture;
             !string.IsNullOrEmpty(culture.Name);
             culture = culture.Parent)
        {
            if (bundles.TryGetValue(culture.Name, out var localeBundles))
            {
                return new ResolvedFluentBundle(localeBundles.Locale, localeBundles.Get(mode));
            }
        }

        var fallback = bundles[options.FallbackLocale.Name];
        return new ResolvedFluentBundle(fallback.Locale, fallback.Get(mode));
    }

    private FrozenDictionary<string, LocaleBundles> LoadCatalog()
    {
        var rootPath = ResolveResourceRoot(options.ResourcesPath);

        if (!Directory.Exists(rootPath))
        {
            throw new TeleFlowConfigurationException(
                $"Fluent resource directory '{rootPath}' does not exist.");
        }

        var catalog = new Dictionary<string, LocaleBundles>(StringComparer.OrdinalIgnoreCase);
        var localeDirectories = Directory
            .EnumerateDirectories(rootPath)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (localeDirectories.Length == 0)
        {
            throw new TeleFlowConfigurationException(
                $"Fluent resource directory '{rootPath}' does not contain locale directories.");
        }

        foreach (var localeDirectory in localeDirectories)
        {
            var directoryName = Path.GetFileName(localeDirectory);

            if (!Locale.TryCreate(directoryName, out var locale))
            {
                throw new TeleFlowConfigurationException(
                    $"Fluent locale directory '{directoryName}' is not a valid locale name.");
            }

            if (catalog.ContainsKey(locale.Name))
            {
                throw new TeleFlowConfigurationException(
                    $"Fluent locale '{locale.Name}' is configured by more than one directory.");
            }

            catalog.Add(locale.Name, LoadLocale(locale, localeDirectory));
        }

        if (!catalog.ContainsKey(options.FallbackLocale.Name))
        {
            throw new TeleFlowConfigurationException(
                $"Fluent fallback locale '{options.FallbackLocale.Name}' does not have a resource catalog.");
        }

        return catalog.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static LocaleBundles LoadLocale(Locale locale, string localeDirectory)
    {
        var resourcePaths = Directory
            .EnumerateFiles(localeDirectory, "*.ftl", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (resourcePaths.Length == 0)
        {
            throw new TeleFlowConfigurationException(
                $"Fluent locale '{locale.Name}' does not contain any .ftl resources.");
        }

        var readers = new (TextReader Reader, string? FileName)[resourcePaths.Length];

        try
        {
            for (var index = 0; index < resourcePaths.Length; index++)
            {
                readers[index] = (File.OpenText(resourcePaths[index]), resourcePaths[index]);
            }

            var ready = LinguiniBuilder
                .Builder()
                .CultureInfo(locale.Culture)
                .AddResources(readers.Select(static item => (item.Reader, item.FileName)))
                .SetUseIsolating(false)
                .UseConcurrent();
            var (baseBundle, errors) = ready.Build();

            if (errors is { Count: > 0 })
            {
                throw CreateResourceException(locale, resourcePaths, errors);
            }

            return new LocaleBundles(
                locale,
                CreateBundle(baseBundle, locale.Culture, FluentRenderingMode.Plain),
                CreateBundle(baseBundle, locale.Culture, FluentRenderingMode.Html),
                CreateBundle(baseBundle, locale.Culture, FluentRenderingMode.MarkdownV2));
        }
        catch (TeleFlowConfigurationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new TeleFlowConfigurationException(
                $"Fluent resources for locale '{locale.Name}' could not be loaded.",
                exception);
        }
        finally
        {
            foreach (var (reader, _) in readers)
            {
                reader?.Dispose();
            }
        }
    }

    private static FrozenBundle CreateBundle(
        FluentBundle baseBundle,
        CultureInfo culture,
        FluentRenderingMode mode)
    {
        var bundle = baseBundle.DeepClone();
        bundle.AddFunctionUnchecked("NUMBER", FluentFunctions.CreateNumber(culture, mode));
        bundle.AddFunctionUnchecked("DATETIME", FluentFunctions.CreateDateTime(culture, mode));
        return bundle.ToFrozenBundle();
    }

    private static TeleFlowConfigurationException CreateResourceException(
        Locale locale,
        IEnumerable<string> resourcePaths,
        IEnumerable<FluentError> errors)
    {
        return new TeleFlowConfigurationException(
            $"Fluent resources for locale '{locale.Name}' are invalid:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, resourcePaths.Select(static path => $"Resource: {path}")) +
            Environment.NewLine +
            string.Join(Environment.NewLine, errors.Select(static error => error.ToString())));
    }

    private static string ResolveResourceRoot(string configuredPath)
    {
        return Path.IsPathRooted(configuredPath)
            ? Path.GetFullPath(configuredPath)
            : Path.GetFullPath(configuredPath, AppContext.BaseDirectory);
    }

    /// <summary>
    /// Groups the immutable plain-text and Telegram-markup bundles built from one parsed locale catalog.
    /// </summary>
    internal sealed record LocaleBundles(
        Locale Locale,
        FrozenBundle Plain,
        FrozenBundle Html,
        FrozenBundle MarkdownV2)
    {
        public FrozenBundle Get(FluentRenderingMode mode)
        {
            return mode switch
            {
                FluentRenderingMode.Plain => Plain,
                FluentRenderingMode.Html => Html,
                FluentRenderingMode.MarkdownV2 => MarkdownV2,
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown Fluent rendering mode.")
            };
        }
    }
}

/// <summary>
/// Couples the concrete locale selected by catalog fallback with its immutable mode-specific Linguini bundle.
/// </summary>
internal readonly record struct ResolvedFluentBundle(Locale Locale, FrozenBundle Bundle);
