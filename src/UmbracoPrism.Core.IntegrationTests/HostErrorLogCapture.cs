using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace UmbracoPrism.Core.IntegrationTests;

/// <summary>
/// An <see cref="ILoggerProvider"/> that buffers every Error/Critical-level log entry the booted
/// host produces, so a test can inspect exactly what was logged (including the exception object,
/// with its stack trace) for an unexpected response — rather than only the status code. ASP.NET
/// Core always logs an unhandled exception via <c>Microsoft.AspNetCore.Hosting.Diagnostics</c>
/// (category "Microsoft.AspNetCore.Hosting.Diagnostics") at Error level as it unwinds to the top
/// of the pipeline, regardless of environment or whether any exception-handling middleware is
/// registered — this provider just needs to be listening when that happens.
/// </summary>
public sealed class HostErrorLogCapture : ILoggerProvider
{
    /// <summary>A captured Error/Critical-level log entry.</summary>
    /// <param name="Category">The logger category (e.g. the type or middleware that logged it).</param>
    /// <param name="Level">Always Error or Critical — see <see cref="Logger.IsEnabled"/>.</param>
    /// <param name="Message">The formatted log message.</param>
    /// <param name="Exception">The logged exception, if any — this is the payload that matters.</param>
    public sealed record Entry(string Category, LogLevel Level, string Message, Exception? Exception)
    {
        public override string ToString() =>
            $"[{Level}] {Category}: {Message}" + (Exception is null ? "" : $"\n{Exception}");
    }

    private readonly ConcurrentQueue<Entry> _entries = new();

    /// <summary>Removes and returns every entry captured since the last drain.</summary>
    public IReadOnlyList<Entry> Drain()
    {
        var drained = new List<Entry>();
        while (_entries.TryDequeue(out var entry))
        {
            drained.Add(entry);
        }

        return drained;
    }

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _entries);

    public void Dispose()
    {
    }

    private sealed class Logger(string category, ConcurrentQueue<Entry> sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            sink.Enqueue(new Entry(category, logLevel, formatter(state, exception), exception));
        }
    }
}
