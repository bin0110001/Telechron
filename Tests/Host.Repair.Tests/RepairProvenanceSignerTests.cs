using Microsoft.Extensions.Options;
using Telechron.Sdk.Repair;

namespace Telechron.Host.Repair.Tests;

// Real ECDSA P-256 sign/verify -- no mocked crypto.
public class RepairProvenanceSignerTests
{
    private static RepairProvenanceRecord SampleRecord() => new(
        RepairAttemptId: Guid.NewGuid(),
        FindingIds: [Guid.NewGuid()],
        CommitReference: "abc123",
        GeneratingPersonaId: null,
        LlmConnectionId: null,
        LlmModelUsed: "gemma4",
        VerifySucceeded: true,
        VerifySummary: "all tests passed",
        SignedAtUtc: DateTimeOffset.UtcNow);

    [Fact]
    public void Sign_ThenVerify_WithSameSignerInstance_Succeeds()
    {
        var signer = new RepairProvenanceSigner(Options.Create(new RepairProvenanceSignerOptions()));
        var record = SampleRecord();

        var signed = signer.Sign(record);

        Assert.True(signer.Verify(signed));
    }

    [Fact]
    public void Verify_TamperedRecord_Fails()
    {
        var signer = new RepairProvenanceSigner(Options.Create(new RepairProvenanceSignerOptions()));
        var record = SampleRecord();
        var signed = signer.Sign(record);

        var tampered = signed with { Record = signed.Record with { CommitReference = "malicious-swap" } };

        Assert.False(signer.Verify(tampered));
    }

    [Fact]
    public void Verify_TamperedSignature_Fails()
    {
        var signer = new RepairProvenanceSigner(Options.Create(new RepairProvenanceSignerOptions()));
        var record = SampleRecord();
        var signed = signer.Sign(record);

        var tampered = signed with { SignatureBase64 = Convert.ToBase64String([1, 2, 3, 4]) };

        Assert.False(signer.Verify(tampered));
    }

    [Fact]
    public void Sign_ProducesDifferentSignaturesAcrossDifferentSignerInstances()
    {
        // Two signer instances with no configured key material each
        // generate their own ephemeral P-256 keypair -- proves the key
        // is genuinely per-instance/session, not some accidental static
        // fallback that would make every unconfigured deployment share
        // one signing identity.
        var signerA = new RepairProvenanceSigner(Options.Create(new RepairProvenanceSignerOptions()));
        var signerB = new RepairProvenanceSigner(Options.Create(new RepairProvenanceSignerOptions()));
        var record = SampleRecord();

        var signedByA = signerA.Sign(record);

        Assert.True(signerA.Verify(signedByA));
        Assert.False(signerB.Verify(signedByA));
    }

    [Fact]
    public void Sign_WithConfiguredPkcs8Key_VerifiesAcrossSeparateSignerInstances()
    {
        using var ecdsa = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var privateKeyBase64 = Convert.ToBase64String(ecdsa.ExportPkcs8PrivateKey());

        var signerA = new RepairProvenanceSigner(Options.Create(new RepairProvenanceSignerOptions { PrivateKeyPkcs8Base64 = privateKeyBase64 }));
        var signerB = new RepairProvenanceSigner(Options.Create(new RepairProvenanceSignerOptions { PrivateKeyPkcs8Base64 = privateKeyBase64 }));

        var signed = signerA.Sign(SampleRecord());

        // Same configured key material -- a second process/instance with
        // the same key from the secret store can verify what the first signed.
        Assert.True(signerB.Verify(signed));
    }
}
