namespace TeleFlow.Telegram.Internal;

internal static class TelegramApiExceptionFactory
{
    public static bool IsThrottling(int statusCode, TelegramTransportEnvelope envelope)
    {
        return statusCode == 429 || envelope.ErrorCode == 429;
    }

    public static TelegramRequestException CreateApiException(
        string methodName,
        int statusCode,
        TelegramTransportEnvelope envelope,
        string? fallbackDescription = null)
    {
        var description = envelope.Description ?? fallbackDescription;
        var message = description is null
            ? $"Telegram request '{methodName}' failed."
            : $"Telegram request '{methodName}' failed: {description}";
        var httpStatusCode = statusCode;
        var telegramErrorCode = envelope.ErrorCode;

        if (envelope.ResponseParameters?.MigrateToChatId is long migrateToChatId)
        {
            return new TelegramMigrateToChatException(
                message,
                methodName,
                httpStatusCode,
                telegramErrorCode,
                description,
                migrateToChatId);
        }

        return statusCode switch
        {
            400 => new TelegramBadRequestException(
                message,
                methodName,
                httpStatusCode,
                telegramErrorCode,
                description),
            401 => new TelegramUnauthorizedException(
                message,
                methodName,
                httpStatusCode,
                telegramErrorCode,
                description),
            403 => new TelegramForbiddenException(
                message,
                methodName,
                httpStatusCode,
                telegramErrorCode,
                description),
            404 => new TelegramNotFoundException(
                message,
                methodName,
                httpStatusCode,
                telegramErrorCode,
                description),
            409 => new TelegramConflictException(
                message,
                methodName,
                httpStatusCode,
                telegramErrorCode,
                description),
            413 => new TelegramEntityTooLargeException(
                message,
                methodName,
                httpStatusCode,
                telegramErrorCode,
                description),
            _ when statusCode >= 500 => new TelegramServerException(
                message,
                methodName,
                httpStatusCode,
                telegramErrorCode,
                description),
            _ => new TelegramApiException(
                message,
                methodName: methodName,
                httpStatusCode: httpStatusCode,
                telegramErrorCode: telegramErrorCode,
                description: description)
        };
    }

}
