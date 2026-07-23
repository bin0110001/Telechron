using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Findings;

public sealed record FailureClassificationResult(FindingFailureClass FailureClass, string Reason);

// R-FIX8: "Flaky infra symptoms -- Stalled/TimedOut Runs, container
// network blips, agent heartbeat loss -- are Environment and never
// become repair candidates; feeding them to the pipeline would generate
// nonsensical 'fixes,' burn caps, and can produce a patch that 'verifies'
// only because the flake didn't recur." R-BUILD4 uses this exact same
// mechanism for synthesis-failure classification (R-ENG4 -- no parallel
// mechanism), so this interface lives in Sdk, not Host/Repair, and is
// deliberately not repair-specific.
public interface IFailureClassifier
{
    FailureClassificationResult Classify(FailureClassificationInput input);
}
