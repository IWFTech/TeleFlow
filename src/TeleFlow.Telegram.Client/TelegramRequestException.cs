using System.Diagnostics.CodeAnalysis;

namespace TeleFlow.Telegram;

[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "Telegram exceptions intentionally require message/context constructors so request diagnostics are not hidden behind generic overloads.")]
public class TelegramException : Exception
{
    public TelegramException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "Telegram request exceptions intentionally keep Telegram context in the primary constructor.")]
public class TelegramRequestException : TelegramException
{
    public TelegramRequestException(
        string message,
        Exception? innerException = null,
        string? methodName = null,
        int? httpStatusCode = null,
        int? telegramErrorCode = null,
        string? description = null,
        int? retryAfterSeconds = null,
        TimeSpan? retryAfter = null,
        long? migrateToChatId = null)
        : base(message, innerException)
    {
        MethodName = methodName;
        HttpStatusCode = httpStatusCode;
        TelegramErrorCode = telegramErrorCode;
        Description = description;
        RetryAfterSeconds = retryAfterSeconds;
        RetryAfter = retryAfter ?? (retryAfterSeconds is null ? null : TimeSpan.FromSeconds(retryAfterSeconds.Value));
        MigrateToChatId = migrateToChatId;
    }

    public string? MethodName { get; }

    public int? HttpStatusCode { get; }

    public int? TelegramErrorCode { get; }

    public string? Description { get; }

    public int? RetryAfterSeconds { get; }

    public TimeSpan? RetryAfter { get; }

    public long? MigrateToChatId { get; }
}

[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "Telegram API exceptions intentionally keep Telegram API context in the primary constructor.")]
public class TelegramApiException : TelegramRequestException
{
    public TelegramApiException(
        string message,
        Exception? innerException = null,
        string? methodName = null,
        int? httpStatusCode = null,
        int? telegramErrorCode = null,
        string? description = null,
        int? retryAfterSeconds = null,
        TimeSpan? retryAfter = null,
        long? migrateToChatId = null)
        : base(
            message,
            innerException,
            methodName,
            httpStatusCode,
            telegramErrorCode,
            description,
            retryAfterSeconds,
            retryAfter,
            migrateToChatId)
    {
    }
}

[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "Telegram status exceptions intentionally keep Telegram API context in the primary constructor.")]
public sealed class TelegramBadRequestException : TelegramApiException
{
    public TelegramBadRequestException(string message, string? methodName = null, int? httpStatusCode = null, int? telegramErrorCode = null, string? description = null)
        : base(message, methodName: methodName, httpStatusCode: httpStatusCode, telegramErrorCode: telegramErrorCode, description: description)
    {
    }
}

[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "Telegram status exceptions intentionally keep Telegram API context in the primary constructor.")]
public sealed class TelegramUnauthorizedException : TelegramApiException
{
    public TelegramUnauthorizedException(string message, string? methodName = null, int? httpStatusCode = null, int? telegramErrorCode = null, string? description = null)
        : base(message, methodName: methodName, httpStatusCode: httpStatusCode, telegramErrorCode: telegramErrorCode, description: description)
    {
    }
}

[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "Telegram status exceptions intentionally keep Telegram API context in the primary constructor.")]
public sealed class TelegramForbiddenException : TelegramApiException
{
    public TelegramForbiddenException(string message, string? methodName = null, int? httpStatusCode = null, int? telegramErrorCode = null, string? description = null)
        : base(message, methodName: methodName, httpStatusCode: httpStatusCode, telegramErrorCode: telegramErrorCode, description: description)
    {
    }
}

[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "Telegram status exceptions intentionally keep Telegram API context in the primary constructor.")]
public sealed class TelegramNotFoundException : TelegramApiException
{
    public TelegramNotFoundException(string message, string? methodName = null, int? httpStatusCode = null, int? telegramErrorCode = null, string? description = null)
        : base(message, methodName: methodName, httpStatusCode: httpStatusCode, telegramErrorCode: telegramErrorCode, description: description)
    {
    }
}

[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "Telegram status exceptions intentionally keep Telegram API context in the primary constructor.")]
public sealed class TelegramConflictException : TelegramApiException
{
    public TelegramConflictException(string message, string? methodName = null, int? httpStatusCode = null, int? telegramErrorCode = null, string? description = null)
        : base(message, methodName: methodName, httpStatusCode: httpStatusCode, telegramErrorCode: telegramErrorCode, description: description)
    {
    }
}

[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "Telegram status exceptions intentionally keep Telegram API context in the primary constructor.")]
public sealed class TelegramEntityTooLargeException : TelegramApiException
{
    public TelegramEntityTooLargeException(string message, string? methodName = null, int? httpStatusCode = null, int? telegramErrorCode = null, string? description = null)
        : base(message, methodName: methodName, httpStatusCode: httpStatusCode, telegramErrorCode: telegramErrorCode, description: description)
    {
    }
}

[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "Telegram throttling exceptions intentionally keep retry metadata in the primary constructor.")]
public sealed class TelegramRetryAfterException : TelegramApiException
{
    public TelegramRetryAfterException(
        string message,
        string? methodName = null,
        int? httpStatusCode = null,
        int? telegramErrorCode = null,
        string? description = null,
        int? retryAfterSeconds = null,
        TimeSpan? retryAfter = null)
        : base(
            message,
            methodName: methodName,
            httpStatusCode: httpStatusCode,
            telegramErrorCode: telegramErrorCode,
            description: description,
            retryAfterSeconds: retryAfterSeconds,
            retryAfter: retryAfter)
    {
    }
}

[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "Telegram migration exceptions intentionally keep migration metadata in the primary constructor.")]
public sealed class TelegramMigrateToChatException : TelegramApiException
{
    public TelegramMigrateToChatException(
        string message,
        string? methodName = null,
        int? httpStatusCode = null,
        int? telegramErrorCode = null,
        string? description = null,
        long? migrateToChatId = null)
        : base(
            message,
            methodName: methodName,
            httpStatusCode: httpStatusCode,
            telegramErrorCode: telegramErrorCode,
            description: description,
            migrateToChatId: migrateToChatId)
    {
    }
}

[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "Telegram status exceptions intentionally keep Telegram API context in the primary constructor.")]
public sealed class TelegramServerException : TelegramApiException
{
    public TelegramServerException(string message, string? methodName = null, int? httpStatusCode = null, int? telegramErrorCode = null, string? description = null)
        : base(message, methodName: methodName, httpStatusCode: httpStatusCode, telegramErrorCode: telegramErrorCode, description: description)
    {
    }
}

[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "Telegram network exceptions intentionally keep method and transport context in the primary constructor.")]
public sealed class TelegramNetworkException : TelegramRequestException
{
    public TelegramNetworkException(
        string message,
        Exception? innerException = null,
        string? methodName = null,
        int? httpStatusCode = null)
        : base(message, innerException, methodName, httpStatusCode)
    {
    }
}

[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "Telegram decode exceptions intentionally keep method, response, and retry context in the primary constructor.")]
public sealed class TelegramDecodeException : TelegramRequestException
{
    public TelegramDecodeException(
        string message,
        Exception? innerException = null,
        string? methodName = null,
        int? httpStatusCode = null,
        int? retryAfterSeconds = null)
        : base(
            message,
            innerException,
            methodName,
            httpStatusCode,
            retryAfterSeconds: retryAfterSeconds)
    {
    }
}
