namespace Telechron.Sdk.Containers;

// R-SYS6: "All synthesized code, module code, untrusted execution, module
// self-tests, repair verification, and workflow executions MUST execute
// inside containerized environments." This is the one execution boundary —
// every Agent-side code path that runs anything beyond its own trusted
// dispatch-handling logic goes through this interface, never a direct
// Process.Start of untrusted content.
public interface IContainerExecutionService
{
    Task<ContainerExecutionResult> ExecuteAsync(ContainerExecutionRequest request, CancellationToken ct = default);
}
