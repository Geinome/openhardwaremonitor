using System;
using Microsoft.Extensions.Logging;

namespace OpenHardwareMonitor;

public static class Logging
{
    private const string DefaultLoggerName = "Errors";

    /// <summary>
    /// The default logger factory for the whole assembly.
    /// If this is null (the default), logging is disabled
    /// </summary>
    public static ILoggerFactory LoggerFactory { get; set; } = null;

    private static ILogger DefaultLogger { get; set; }

    private static ILogger GetLogger(string loggerName)
    {
        if (LoggerFactory == null) return new NullLogger();

        return LoggerFactory.CreateLogger(loggerName);
    }

    /// <summary>
    /// Gets a logger with the name of the current class
    /// </summary>
    /// <param name="currentClass">The class whose logger shall be retrieved</param>
    /// <returns>A <see cref="ILogger"/> instance</returns>
    public static ILogger GetCurrentClassLogger(this object currentClass)
    {
        var name = currentClass.GetType().FullName;

        // This is true if the method is used from an incomplete (generic) type
        if (name == null) name = currentClass.GetType().Name;

        return GetLogger(name);
    }

    public static void LogError(Exception ex, string message)
    {
        if (LoggerFactory == null) return;

        if (DefaultLogger == null) DefaultLogger = LoggerFactory.CreateLogger(DefaultLoggerName);

        DefaultLogger.LogError(ex, message);
    }

    public static void LogInfo(string message)
    {
        if (LoggerFactory == null) return;

        if (DefaultLogger == null) DefaultLogger = LoggerFactory.CreateLogger(DefaultLoggerName);

        DefaultLogger.LogInformation(message);
    }

    private class NullLogger : ILogger
    {
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter)
        {
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return false;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return new ScopeDisposable();
        }
    }

    internal sealed class ScopeDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
