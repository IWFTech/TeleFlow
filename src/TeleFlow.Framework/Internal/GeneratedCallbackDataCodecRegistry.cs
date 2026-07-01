using System.Collections.Concurrent;
using System.Reflection;

namespace TeleFlow.Telegram.Internal;

/// <summary>
/// Discovers and caches source-generated compact callback data codecs from payload
/// assemblies so keyboard packing and callback routing can share the generated path.
/// </summary>
internal static class GeneratedCallbackDataCodecRegistry
{
    private static readonly ConcurrentDictionary<Assembly, Lazy<AssemblyCodecLookup>> AssemblyCache = new();

    public static bool TryGet(Type payloadType, out GeneratedCallbackDataCodec codec)
    {
        ArgumentNullException.ThrowIfNull(payloadType);

        var lookup = AssemblyCache.GetOrAdd(
            payloadType.Assembly,
            static assembly => new Lazy<AssemblyCodecLookup>(
                () => CreateAssemblyLookup(assembly),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;

        return lookup.TryGet(payloadType, out codec);
    }

    private static AssemblyCodecLookup CreateAssemblyLookup(Assembly assembly)
    {
        var attributes = assembly
            .GetCustomAttributes<TelegramGeneratedCallbackDataCodecsAttribute>()
            .OrderBy(static attribute => attribute.RegistrarType.FullName, StringComparer.Ordinal)
            .ToArray();

        if (attributes.Length == 0)
        {
            return AssemblyCodecLookup.Empty;
        }

        var registry = new Registry();

        foreach (var attribute in attributes)
        {
            var registrar = CreateRegistrar(attribute.RegistrarType);

            registrar.RegisterCallbackDataCodecs(registry);
        }

        return registry.ToLookup();
    }

    private static ITelegramGeneratedCallbackDataCodecRegistrar CreateRegistrar(Type registrarType)
    {
        if (!typeof(ITelegramGeneratedCallbackDataCodecRegistrar).IsAssignableFrom(registrarType))
        {
            throw new InvalidOperationException(
                $"Generated Telegram callback data codec registrar '{registrarType.FullName}' must implement {nameof(ITelegramGeneratedCallbackDataCodecRegistrar)}.");
        }

        if (Activator.CreateInstance(registrarType) is not ITelegramGeneratedCallbackDataCodecRegistrar registrar)
        {
            throw new InvalidOperationException(
                $"Generated Telegram callback data codec registrar '{registrarType.FullName}' could not be created.");
        }

        return registrar;
    }

    private sealed class Registry : ITelegramGeneratedCallbackDataCodecRegistry
    {
        private readonly List<GeneratedCallbackDataCodec> _codecs = [];

        public void RegisterCallbackDataCodec(TelegramGeneratedCallbackDataCodecDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);

            _codecs.Add(new GeneratedCallbackDataCodec(descriptor));
        }

        public AssemblyCodecLookup ToLookup()
        {
            return AssemblyCodecLookup.Create(_codecs);
        }
    }

    private sealed class AssemblyCodecLookup
    {
        public static readonly AssemblyCodecLookup Empty = new(
            new Dictionary<Type, GeneratedCallbackDataCodec>());

        private readonly IReadOnlyDictionary<Type, GeneratedCallbackDataCodec> _codecs;

        private AssemblyCodecLookup(IReadOnlyDictionary<Type, GeneratedCallbackDataCodec> codecs)
        {
            _codecs = codecs;
        }

        public static AssemblyCodecLookup Create(IReadOnlyList<GeneratedCallbackDataCodec> codecs)
        {
            var duplicateType = codecs
                .GroupBy(static codec => codec.PayloadType)
                .FirstOrDefault(static group => group.Count() > 1);

            if (duplicateType is not null)
            {
                throw new InvalidOperationException(
                    $"Generated Telegram callback data payload type '{duplicateType.Key.FullName}' was registered more than once.");
            }

            var duplicatePrefix = codecs
                .GroupBy(static codec => codec.Prefix, StringComparer.Ordinal)
                .FirstOrDefault(static group => group.Count() > 1);

            if (duplicatePrefix is not null)
            {
                var payloadTypes = string.Join(
                    ", ",
                    duplicatePrefix.Select(static codec => codec.PayloadType.FullName));

                throw new InvalidOperationException(
                    $"Generated Telegram callback data prefix '{duplicatePrefix.Key}' is used by multiple payload types: {payloadTypes}.");
            }

            return new AssemblyCodecLookup(codecs.ToDictionary(static codec => codec.PayloadType));
        }

        public bool TryGet(Type payloadType, out GeneratedCallbackDataCodec codec)
        {
            return _codecs.TryGetValue(payloadType, out codec!);
        }
    }
}

/// <summary>
/// Runtime wrapper around generated callback data delegates that validates payload
/// compatibility and Telegram callback data size at framework boundaries.
/// </summary>
internal sealed class GeneratedCallbackDataCodec
{
    private readonly TelegramGeneratedCallbackDataCodecDescriptor _descriptor;

    public GeneratedCallbackDataCodec(TelegramGeneratedCallbackDataCodecDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        _descriptor = descriptor;
    }

    public Type PayloadType => _descriptor.PayloadType;

    public string Prefix => _descriptor.Prefix;

    public string Pack(object payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (!PayloadType.IsInstanceOfType(payload))
        {
            throw new InvalidOperationException(
                $"Callback data payload type '{payload.GetType().FullName}' is not compatible with generated codec for '{PayloadType.FullName}'.");
        }

        var serializedPayload = _descriptor.Packer(payload);
        CallbackDataCodec.ValidateCallbackData(serializedPayload, "Serialized Telegram callback data");
        return serializedPayload;
    }

    public bool MatchesSerializedPayload(string serializedPayload)
    {
        ArgumentNullException.ThrowIfNull(serializedPayload);

        return _descriptor.Matcher(serializedPayload);
    }

    public object Unpack(string serializedPayload)
    {
        ArgumentNullException.ThrowIfNull(serializedPayload);

        return _descriptor.Unpacker(serializedPayload);
    }
}
