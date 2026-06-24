namespace TeleFlow.Telegram.Internal;

internal static class TelegramKeyboardPlaceholderValidator
{
    private const int InputFieldPlaceholderLimit = 64;

    public static void ValidateReplyKeyboardInputFieldPlaceholder(string placeholder)
    {
        ValidateInputFieldPlaceholder(
            placeholder,
            "Reply keyboard input placeholder must be at most 64 characters.");
    }

    public static void ValidateForceReplyInputFieldPlaceholder(string placeholder)
    {
        ValidateInputFieldPlaceholder(
            placeholder,
            "Force reply input placeholder must be at most 64 characters.");
    }

    private static void ValidateInputFieldPlaceholder(string placeholder, string limitMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(placeholder);

        if (placeholder.Length > InputFieldPlaceholderLimit)
        {
            throw new ArgumentException(limitMessage, nameof(placeholder));
        }
    }
}
