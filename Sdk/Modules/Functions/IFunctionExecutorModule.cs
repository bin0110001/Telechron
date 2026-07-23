namespace Telechron.Sdk.Modules.Functions;

// R-WF1/R-WF2: "Workflow execution is function driven; function executors
// are hot-reloadable." A Function executor declares which Kinds it can
// run (matching Function.Kind, R-DM4) and whether that Kind's work is
// safe to run in-process (pure data transforms with no filesystem/
// network/process-execution touch -- e.g. a JSON reshape) or must be
// dispatched into a container (R-SYS6 -- e.g. Git/Zip/Upload/Deploy,
// which touch the filesystem or network). InvokeInProcessAsync is only
// ever called for a Kind the module itself reports as RequiresContainer
// == false; the Host enforces that split, the module doesn't self-police
// it.
public interface IFunctionExecutorModule : IModule
{
    IReadOnlyList<string> SupportedFunctionKinds { get; }

    bool RequiresContainer(string functionKind);

    // Only called when RequiresContainer(functionKind) == false.
    Task<FunctionInvocationResult> InvokeInProcessAsync(
        string functionKind, string inputArtifactTypesJson, string parametersJson, CancellationToken ct = default);

    // For a container-required Kind, returns the command to dispatch
    // (matching the same "descriptor, not executor" pattern as
    // IToolchainModule) -- the Host builds and dispatches the actual
    // ContainerExecutionRequest from this, the module never touches the
    // container itself.
    IReadOnlyList<string> BuildContainerCommand(string functionKind, string inputArtifactTypesJson, string parametersJson);
}
