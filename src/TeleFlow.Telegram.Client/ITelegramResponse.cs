using System.Diagnostics.CodeAnalysis;

namespace TeleFlow.Telegram;

[SuppressMessage(
    "Design",
    "CA1040:Avoid empty interfaces",
    Justification = "Telegram responses use this marker to keep generated client method results inside the Telegram response model.")]
public interface ITelegramResponse
{
}
