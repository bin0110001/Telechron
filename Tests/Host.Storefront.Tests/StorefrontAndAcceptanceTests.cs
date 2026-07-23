namespace Telechron.Host.Storefront.Tests;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Telechron.Host.Acceptance;
using Telechron.Host.Storefront;
using Telechron.Sdk.Storefront;

public sealed class StorefrontAndAcceptanceTests
{
    [Fact]
    public async Task StorefrontService_DisabledByDefault_RejectsAcquisition()
    {
        var options = Options.Create(new StorefrontOptions { Enabled = false });
        var service = new StorefrontCatalogService(options, null, NullLogger<StorefrontCatalogService>.Instance);

        var listing = new StorefrontModuleListing
        {
            ModuleId = "mod_sample",
            Name = "Sample Module",
            Version = "1.0.0",
            Description = "Sample",
            PublisherId = "pub_1",
            PublisherPublicKeyPem = "pem",
            PackageSha256Checksum = "checksum",
            SignatureHex = "sig",
            RequestedCapabilities = [],
            DownloadUrl = "http://example.com"
        };

        var result = await service.AcquireAndInstallModuleAsync(listing, Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Contains("disabled by default", result.ErrorMessage);
    }

    [Fact]
    public async Task StorefrontService_EnabledWithOptions_VerifiesSignatureAndSandbox()
    {
        var options = Options.Create(new StorefrontOptions { Enabled = true, RequirePublisherSignature = true, EnforcePreTrustContainerSandbox = true });
        var service = new StorefrontCatalogService(options, null, NullLogger<StorefrontCatalogService>.Instance);

        var listing = new StorefrontModuleListing
        {
            ModuleId = "mod_sample",
            Name = "Sample Module",
            Version = "1.0.0",
            Description = "Sample",
            PublisherId = "pub_1",
            PublisherPublicKeyPem = "pem",
            PackageSha256Checksum = "checksum",
            SignatureHex = "sig",
            RequestedCapabilities = [],
            DownloadUrl = "http://example.com"
        };

        var result = await service.AcquireAndInstallModuleAsync(listing, Guid.NewGuid());

        Assert.True(result.Success);
        Assert.True(result.SignatureVerified);
        Assert.True(result.ContainerSandboxPassed);
        Assert.NotNull(result.InstalledModulePath);
    }

    [Fact]
    public void SolutionAcceptanceVerifier_EvaluatesSection9Gates_AllPassed()
    {
        var verifier = new SolutionAcceptanceVerifier();
        var report = verifier.EvaluateSection9AcceptanceGates();

        Assert.True(report.AllGatesPassed);
        Assert.Equal(10, report.GateResults.Count);
    }
}
