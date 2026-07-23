namespace Telechron.Host.Modules;

public enum ModuleTrustOutcome
{
    Trusted,
    IntegrityFailed,
    CapabilityNotApproved,
    PreTrustSelfTestFailed,
    FalsifiabilityCheckFailed,
}

public sealed record ModuleTrustResult(ModuleTrustOutcome Outcome, string Reason)
{
    public bool IsTrusted => Outcome == ModuleTrustOutcome.Trusted;
}
