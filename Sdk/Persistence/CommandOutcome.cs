namespace Telechron.Sdk.Persistence;

// The Agent-reported outcome of one dispatched command (mirrors
// CommandResultRequest on the wire) — the payload a correlator hands back
// to whatever Host-side caller is awaiting this specific CommandId.
public sealed record CommandOutcome(Guid CommandId, bool Succeeded, string OutputSummary, string ErrorMessage);
