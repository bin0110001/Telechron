using System.Text;
using Microsoft.Extensions.Logging;
using Telechron.Host.Security.Logging;

namespace Telechron.Host.Security.Tests.Logging;

public sealed class RedactingLoggerProviderTests
{
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> CapturedMessages { get; } = [];

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose() { }

        private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                owner.CapturedMessages.Add(formatter(state, exception));
        }
    }

    [Fact]
    public void Log_RedactsSecretValue_BeforeReachingInnerProvider()
    {
        var fingerprints = new SecretFingerprintRegistry();
        var inner = new CapturingLoggerProvider();
        var provider = new RedactingLoggerProvider(inner, fingerprints);
        var logger = provider.CreateLogger("Test");
        var secretText = "leaked-secret-in-exception-message";

        using (fingerprints.Track(Encoding.UTF8.GetBytes(secretText)))
        {
            logger.LogInformation("Connector call failed: {Detail}", secretText);
        }

        Assert.Single(inner.CapturedMessages);
        Assert.DoesNotContain(secretText, inner.CapturedMessages[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Log_WithNoActiveSecrets_PassesMessageThroughUnchanged()
    {
        var fingerprints = new SecretFingerprintRegistry();
        var inner = new CapturingLoggerProvider();
        var provider = new RedactingLoggerProvider(inner, fingerprints);
        var logger = provider.CreateLogger("Test");

        logger.LogInformation("Ordinary log message.");

        Assert.Equal("Ordinary log message.", inner.CapturedMessages.Single());
    }
}
