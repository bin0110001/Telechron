namespace Telechron.Sdk.Modules;

public sealed record IntegrityVerificationResult(bool IsValid, string Reason);

// R-MOD5a: "MUST be signed by a known publisher key; the Host verifies
// signature and checksum before installation and refuses load on
// mismatch." Runs before ModuleRuntime.LoadAsync ever touches the
// assembly bytes -- a module's own self-test is explicitly NOT a security
// attestation (R-MOD5a), so this check cannot be satisfied by anything
// the module itself reports.
public interface IModuleIntegrityVerifier
{
    Task<IntegrityVerificationResult> VerifyAsync(
        string moduleAssemblyPath, ModuleIntegrityManifest manifest, CancellationToken ct = default);
}
