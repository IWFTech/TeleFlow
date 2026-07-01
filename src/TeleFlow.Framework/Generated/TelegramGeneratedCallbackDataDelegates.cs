using System.ComponentModel;

namespace TeleFlow.Telegram;

/// <summary>
/// Infrastructure delegate emitted into generated callback data codec descriptors to pack
/// a compact callback payload without runtime field reflection.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public delegate string TelegramGeneratedCallbackDataPacker(object payload);

/// <summary>
/// Infrastructure delegate emitted into generated callback data codec descriptors to check
/// whether a Telegram callback string belongs to the generated payload type.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public delegate bool TelegramGeneratedCallbackDataMatcher(string serializedPayload);

/// <summary>
/// Infrastructure delegate emitted into generated callback data codec descriptors to unpack
/// a compact callback payload without runtime constructor or property reflection.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public delegate object TelegramGeneratedCallbackDataUnpacker(string serializedPayload);
