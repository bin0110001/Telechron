namespace Telechron.Sdk.Repair;

// R-FIX9: repair targets (here, a Project's root path, since Snapshot/
// Apply/Verify/Commit all operate against one working tree at a time) are
// exclusively locked for the duration of a repair pipeline instance;
// concurrent attempts against overlapping targets are queued, not run in
// parallel. Returns an IAsyncDisposable so the orchestrator can `await
// using` it and guarantee release even on an exception mid-pipeline.
public interface IRepairConcurrencyGate
{
    Task<IAsyncDisposable> AcquireAsync(string projectRootPath, CancellationToken ct = default);
}
