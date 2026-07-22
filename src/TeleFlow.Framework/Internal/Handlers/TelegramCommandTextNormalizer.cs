using System.Text;

namespace TeleFlow.Telegram.Internal.Handlers;

/// <summary>
/// Canonicalizes command route patterns and incoming command bodies so visually
/// equivalent Unicode sequences use the same runtime matching representation.
/// </summary>
internal static class TelegramCommandTextNormalizer
{
    public static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.IsNormalized(NormalizationForm.FormC)
            ? value
            : value.Normalize(NormalizationForm.FormC);
    }
}
