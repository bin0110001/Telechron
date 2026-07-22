using System.Text;
using Telechron.Host.Security.Logging;

namespace Telechron.Host.Security.Tests.Logging;

public sealed class SecretFingerprintRegistryTests
{
    [Fact]
    public void TryRedact_ReplacesTrackedValue_WhilePresentInScope()
    {
        var registry = new SecretFingerprintRegistry();
        var secretText = "super-secret-api-key-value";

        using (registry.Track(Encoding.UTF8.GetBytes(secretText)))
        {
            var redacted = registry.TryRedact($"Calling API with key {secretText}", out var result);

            Assert.True(redacted);
            Assert.DoesNotContain(secretText, result, StringComparison.Ordinal);
            Assert.Contains("[REDACTED-SECRET]", result, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TryRedact_NoLongerRedacts_AfterScopeDisposed()
    {
        var registry = new SecretFingerprintRegistry();
        var secretText = "another-secret-value-here";

        using (registry.Track(Encoding.UTF8.GetBytes(secretText)))
        {
            // in scope
        }

        var redacted = registry.TryRedact($"Message containing {secretText}", out var result);

        Assert.False(redacted);
        Assert.Contains(secretText, result, StringComparison.Ordinal);
    }

    [Fact]
    public void TryRedact_MessageWithoutSecret_IsUnchanged()
    {
        var registry = new SecretFingerprintRegistry();
        using (registry.Track(Encoding.UTF8.GetBytes("tracked-secret-value")))
        {
            var redacted = registry.TryRedact("A completely unrelated log message.", out var result);

            Assert.False(redacted);
            Assert.Equal("A completely unrelated log message.", result);
        }
    }

    [Fact]
    public void Track_VeryShortValue_IsNotTracked_ToAvoidFalsePositiveRedaction()
    {
        var registry = new SecretFingerprintRegistry();
        using (registry.Track(Encoding.UTF8.GetBytes("ab")))
        {
            var redacted = registry.TryRedact("The word ab appears here normally.", out var result);

            Assert.False(redacted);
        }
    }
}
