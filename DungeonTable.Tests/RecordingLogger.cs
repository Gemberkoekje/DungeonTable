using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace DungeonTable.Tests;

/// <summary>A logger that keeps what it is told, so a test can read the log back.</summary>
/// <typeparam name="T">The category.</typeparam>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<(LogLevel Level, string Message)> entries = new();

    /// <summary>Everything logged, in order.</summary>
    public IReadOnlyList<(LogLevel Level, string Message)> Entries => entries.ToArray();

    /// <summary>The messages logged at one level, in order.</summary>
    /// <param name="level">The level.</param>
    /// <returns>The messages.</returns>
    public IReadOnlyList<string> At(LogLevel level) =>
        entries.Where(entry => entry.Level == level).Select(entry => entry.Message).ToArray();

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);
        entries.Enqueue((logLevel, formatter(state, exception)));
    }
}
