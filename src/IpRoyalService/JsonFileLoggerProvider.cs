using System.Text.Json;

namespace IpRoyalService;
public sealed class JsonFileLoggerProvider(string path) : ILoggerProvider
{
    private readonly object gate = new();
    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);
    public void Dispose() { }

    private void Write(string category, LogLevel level, EventId eventId, string message, Exception? exception)
    {
        try
        {
            var directory = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(directory);
            var entry = JsonSerializer.Serialize(new { timestamp = DateTimeOffset.UtcNow, level = level.ToString(), category, eventId = eventId.Id, message, exception = exception?.GetType().Name });
            lock (gate) File.AppendAllText(path, entry + Environment.NewLine);
        }
        catch { /* Logging must never terminate enforcement. */ }
    }

    private sealed class FileLogger(JsonFileLoggerProvider owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { if (IsEnabled(level)) owner.Write(category, level, eventId, formatter(state, exception), exception); }
    }
}
