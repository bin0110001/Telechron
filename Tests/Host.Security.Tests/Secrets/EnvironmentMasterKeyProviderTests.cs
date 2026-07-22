using Telechron.Host.Security.Secrets;

namespace Telechron.Host.Security.Tests.Secrets;

// Not run in parallel with other tests that touch these env vars, since
// EnvironmentMasterKeyProvider reads process-wide state.
[Collection("EnvironmentMasterKeyProvider")]
public sealed class EnvironmentMasterKeyProviderTests : IDisposable
{
    private readonly List<string> _setVars = [];

    private void SetEnv(string name, string? value)
    {
        Environment.SetEnvironmentVariable(name, value);
        _setVars.Add(name);
    }

    public void Dispose()
    {
        foreach (var name in _setVars)
            Environment.SetEnvironmentVariable(name, null);
    }

    [Fact]
    public void MissingKey_Throws()
    {
        SetEnv("TELECHRON_MASTER_KEY", null);
        SetEnv("TELECHRON_MASTER_KEY_FILE", null);

        Assert.Throws<InvalidOperationException>(() => new EnvironmentMasterKeyProvider());
    }

    [Fact]
    public void ConfiguredKey_IsRetrievableAsCurrentKey()
    {
        var keyBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        SetEnv("TELECHRON_MASTER_KEY", Convert.ToBase64String(keyBytes));

        var provider = new EnvironmentMasterKeyProvider();

        Assert.Equal(keyBytes, provider.GetCurrentKey().ToArray());
    }

    [Fact]
    public void CustomKeyId_IsRespected()
    {
        var keyBytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        SetEnv("TELECHRON_MASTER_KEY", Convert.ToBase64String(keyBytes));
        SetEnv("TELECHRON_MASTER_KEY_ID", "v7");

        var provider = new EnvironmentMasterKeyProvider();

        Assert.Equal("v7", provider.CurrentKeyId);
    }

    [Fact]
    public void UnknownKeyId_Throws()
    {
        SetEnv("TELECHRON_MASTER_KEY", Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)));

        var provider = new EnvironmentMasterKeyProvider();

        Assert.Throws<InvalidOperationException>(() => provider.GetKey("nonexistent"));
    }
}
