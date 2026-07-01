using System.ComponentModel;

namespace TeleFlow.Telegram;

/// <summary>
/// Infrastructure callback data codec metadata emitted by TeleFlow source generators.
/// It connects a payload type to direct generated pack, match, and unpack delegates.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TelegramGeneratedCallbackDataCodecDescriptor
{
    public TelegramGeneratedCallbackDataCodecDescriptor(
        Type payloadType,
        string prefix,
        TelegramGeneratedCallbackDataPacker packer,
        TelegramGeneratedCallbackDataMatcher matcher,
        TelegramGeneratedCallbackDataUnpacker unpacker)
    {
        ArgumentNullException.ThrowIfNull(payloadType);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        ArgumentNullException.ThrowIfNull(packer);
        ArgumentNullException.ThrowIfNull(matcher);
        ArgumentNullException.ThrowIfNull(unpacker);

        PayloadType = payloadType;
        Prefix = prefix;
        Packer = packer;
        Matcher = matcher;
        Unpacker = unpacker;
    }

    public Type PayloadType { get; }

    public string Prefix { get; }

    public TelegramGeneratedCallbackDataPacker Packer { get; }

    public TelegramGeneratedCallbackDataMatcher Matcher { get; }

    public TelegramGeneratedCallbackDataUnpacker Unpacker { get; }
}
