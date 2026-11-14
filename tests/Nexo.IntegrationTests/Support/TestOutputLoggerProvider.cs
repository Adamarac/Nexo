using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Nexo.IntegrationTests.Support;

/// <summary>Manda avisos e erros dos workers para a saída do teste, para entender uma falha sem depurar.</summary>
public sealed class TestOutputLoggerProvider(ITestOutputHelper output) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new TestOutputLogger(output, categoryName);

    public void Dispose()
    {
    }

    private sealed class TestOutputLogger(ITestOutputHelper output, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            try
            {
                output.WriteLine($"[{logLevel}] {category}: {formatter(state, exception)} {exception?.GetType().Name}: {exception?.Message}");
            }
            catch (InvalidOperationException)
            {
                // O teste já terminou; não há mais onde escrever.
            }
        }
    }
}
