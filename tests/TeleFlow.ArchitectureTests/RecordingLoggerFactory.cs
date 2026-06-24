using Microsoft.Extensions.Logging;

namespace TeleFlow.ArchitectureTests;

internal sealed class RecordingLoggerFactory : ILoggerFactory
{
    public List<TestLogEntry> Entries { get; } = [];

    public ILogger CreateLogger(string categoryName)
    {
        return new RecordingLogger(categoryName, Entries);
    }

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }

    private sealed class RecordingLogger(
        string categoryName,
        List<TestLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Add(new TestLogEntry(categoryName, logLevel, formatter(state, exception), exception));
        }
    }
}

internal sealed record TestLogEntry(
    string Category,
    LogLevel Level,
    string Message,
    Exception? Exception);
