using Microsoft.Extensions.Options;
using Telechron.Agent.Containers;

namespace Telechron.Agent.Containers.Tests;

public class ImageProvenanceVerifierTests
{
    private static ImageProvenanceVerifier CreateVerifier(params string[] allowedRegistries)
    {
        var options = Options.Create(new RegistryAllowlist { AllowedRegistries = allowedRegistries });
        return new ImageProvenanceVerifier(options);
    }

    [Theory]
    [InlineData("mcr.microsoft.com/dotnet/aspnet@sha256:6391fb08009d28f9a74df93ab08711082041d4c79672a4354fbe605ddb817fa1")]
    [InlineData("docker.io/library/alpine@sha256:1e42bbe2508154c9126d48c2b8a75420c3544343bf86fd041fb7527e017a4b4d")]
    public void Verify_DigestPinnedFromAllowlistedRegistry_IsAllowed(string reference)
    {
        var verifier = CreateVerifier("mcr.microsoft.com", "docker.io/library");

        var result = verifier.Verify(reference);

        Assert.True(result.IsAllowed);
    }

    [Theory]
    [InlineData("mcr.microsoft.com/dotnet/aspnet:8.0")]
    [InlineData("mcr.microsoft.com/dotnet/aspnet:latest")]
    [InlineData("mcr.microsoft.com/dotnet/aspnet")]
    public void Verify_MutableTagReference_IsRejected(string reference)
    {
        var verifier = CreateVerifier("mcr.microsoft.com");

        var result = verifier.Verify(reference);

        Assert.False(result.IsAllowed);
        Assert.Contains("not a digest-pinned reference", result.Reason);
    }

    [Fact]
    public void Verify_DigestPinnedFromNonAllowlistedRegistry_IsRejected()
    {
        var verifier = CreateVerifier("mcr.microsoft.com");

        var result = verifier.Verify("evil.example.com/malware@sha256:1e42bbe2508154c9126d48c2b8a75420c3544343bf86fd041fb7527e017a4b4d");

        Assert.False(result.IsAllowed);
        Assert.Contains("not in the allowlist", result.Reason);
    }

    [Fact]
    public void Verify_RegistryWithPathPrefixAllowlistEntry_RequiresPathMatch()
    {
        var verifier = CreateVerifier("docker.io/library");

        var allowed = verifier.Verify("docker.io/library/alpine@sha256:1e42bbe2508154c9126d48c2b8a75420c3544343bf86fd041fb7527e017a4b4d");
        var rejected = verifier.Verify("docker.io/someoneelse/alpine@sha256:1e42bbe2508154c9126d48c2b8a75420c3544343bf86fd041fb7527e017a4b4d");

        Assert.True(allowed.IsAllowed);
        Assert.False(rejected.IsAllowed);
    }

    [Fact]
    public void Verify_ShortOrMalformedDigest_IsRejected()
    {
        var verifier = CreateVerifier("mcr.microsoft.com");

        var result = verifier.Verify("mcr.microsoft.com/dotnet/aspnet@sha256:deadbeef");

        Assert.False(result.IsAllowed);
    }
}
