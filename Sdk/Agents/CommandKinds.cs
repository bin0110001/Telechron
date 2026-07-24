namespace Telechron.Sdk.Agents;

// R-SYS2's dispatched-work examples ("Run tests, Build projects, Execute
// workflows, Repair code, Manage GPU resources") given stable string IDs.
// Shared between Host (CommandDispatchValidator's schema registry is keyed
// by these) and Agent (command handler registration) — lives in Sdk since
// neither project references the other. Extend deliberately.
public static class CommandKinds
{
    public const string RunTests = "run-tests";
    public const string Build = "build";
    public const string ExecuteWorkflowStep = "execute-workflow-step";
    public const string ApplyRepairPatch = "apply-repair-patch";
    // R-MOD4/R-MOD4a/R-MOD5b: runs a module's self-test inside a container
    // (R-SYS6 -- never in-process on the Host, ALC load is lifecycle-only
    // per R-MOD7). ModuleAssemblyBlobRef points to the exact assembly build
    // to test -- pre-patch or post-patch snapshot, or the version pending
    // pre-trust sandbox approval.
    public const string RunModuleSelfTest = "run-module-self-test";
    // R-FIX2 Verify stage: fetches a zipped repair working-tree snapshot
    // (workspaceBlobRef) from the Host, unpacks it, and runs the Project's
    // Toolchain TestCommand inside a container (R-SYS6) -- the container
    // execution boundary Verify needs but that only exists on the Agent.
    public const string RunRepairVerify = "run-repair-verify";
    // R-BUILD5: builds a Design-Doc-informed, LLM-synthesized module
    // (source + self-test + a minimal .csproj referencing Telechron.Sdk,
    // zipped as sourceBundleBlobRef) inside a container via `dotnet build`
    // and `dotnet test`, then uploads the resulting assembly back to the
    // Host via StoreArtifact so IModuleTrustEvaluator can run its real
    // pre-trust pipeline (R-MOD5a/R-MOD5b) against a real compiled DLL,
    // never against unverified source text.
    public const string RunCapabilitySynthesisBuild = "run-capability-synthesis-build";
}
