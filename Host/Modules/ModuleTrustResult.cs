namespace Telechron.Host.Modules;

public enum ModuleTrustOutcome
{
    Trusted,
    IntegrityFailed,
    CapabilityNotApproved,
    PreTrustSelfTestFailed,
    FalsifiabilityCheckFailed,
    // R-DM7a: a differing-major-version update was presented without the
    // caller asserting it already has separate re-approval (versionReapproved).
    MajorVersionRequiresReapproval,
}

public sealed record ModuleTrustResult(ModuleTrustOutcome Outcome, string Reason)
{
    public bool IsTrusted => Outcome == ModuleTrustOutcome.Trusted;
}
