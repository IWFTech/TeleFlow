using Microsoft.Extensions.Logging;

namespace TeleFlow.ArchitectureTests;

internal sealed class RecordingLoggerFactory(LogLevel minimumLevel = LogLevel.Trace) : ILoggerFactory
{
    public List<TestLogEntry> Entries { get; } = [];

    public ILogger CreateLogger(string categoryName)
    {
        return new RecordingLogger(categoryName, minimumLevel, Entries);
    }

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }

    private sealed class RecordingLogger(
        string categoryName,
        LogLevel minimumLevel,
        List<TestLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= minimumLevel && minimumLevel != LogLevel.None;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Add(new TestLogEntry(categoryName, eventId, logLevel, formatter(state, exception), exception));
        }
    }
}

internal sealed record TestLogEntry(
    string Category,
    EventId EventId,
    LogLevel Level,
    string Message,
    Exception? Exception);
