namespace Telechron.Sdk.Domain;

// R-FIX8: only Code-classified Findings become repair candidates.
// Environment (Stalled/TimedOut Runs, network blips) route to retry/quarantine.
public enum FindingFailureClass
{
    Environment = 0,
    Code = 1,
}
