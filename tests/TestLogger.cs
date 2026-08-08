// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// ------------------------------------------------------------

namespace OpcPlc.Tests;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using System;
using System.IO;

/// <summary>
/// Logger provider that writes host log output to the NUnit progress writer.
/// </summary>
public sealed class TestLoggerProvider : ILoggerProvider
{
    private readonly TextWriter _outputWriter;

    public TestLoggerProvider(TextWriter outputWriter)
    {
        _outputWriter = outputWriter;
    }

    public ILogger CreateLogger(string categoryName) => new TestOutputLogger(categoryName, _outputWriter);

    public void Dispose()
    {
    }

    private sealed class TestOutputLogger : ILogger
    {
        private readonly string _category;
        private readonly TextWriter _outputWriter;

        public TestOutputLogger(string category, TextWriter outputWriter)
        {
            _category = category;
            _outputWriter = outputWriter;
        }

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            try
            {
                _outputWriter.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff} [{logLevel}] {_category}: {formatter(state, exception)}{(exception is null ? string.Empty : Environment.NewLine + exception)}");
            }
            catch (ObjectDisposedException)
            {
                // The test run may already be finished.
            }
        }
    }
}

public class TestLogger<T> : ILogger<T>
{
    private readonly TextWriter _outputWriter;
    private readonly ConsoleFormatter _formatter;
    private readonly string _category = typeof(T).FullName;

    public TestLogger(TextWriter outputWriter, ConsoleFormatter formatter)
    {
        _outputWriter = outputWriter;
        _formatter = formatter;
    }

    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Debug;

    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= MinimumLogLevel;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        if (logLevel < MinimumLogLevel)
        {
            return;
        }

        _formatter.Write(new LogEntry<TState>(logLevel, _category, eventId, state, exception, formatter), null, _outputWriter);
    }
}
