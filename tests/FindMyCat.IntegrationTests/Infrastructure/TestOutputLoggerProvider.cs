using Microsoft.Extensions.Logging;

namespace FindMyCat.IntegrationTests.Infrastructure;

public sealed class TestOutputLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new TestOutputLogger(categoryName);

    public void Dispose()
    {
    }
}

internal sealed class TestOutputLogger(string categoryName) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;

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

        var output = IntegrationTestBase.CurrentOutput;

        if (output is null)
        {
            return;
        }

        try
        {
            output.WriteLine($"[{logLevel}] {categoryName}: {formatter(state, exception)}");

            if (exception is not null)
            {
                output.WriteLine($"Exception: {exception}");
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}
