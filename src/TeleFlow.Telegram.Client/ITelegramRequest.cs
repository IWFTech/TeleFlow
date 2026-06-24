using System.Diagnostics.CodeAnalysis;

namespace TeleFlow.Telegram;

[SuppressMessage(
    "Design",
    "CA1040:Avoid empty interfaces",
    Justification = "Telegram requests use this marker as the typed client boundary between public method contracts and the executor.")]
public interface ITelegramRequest<out TResponse>
    where TResponse : ITelegramResponse
{
}
