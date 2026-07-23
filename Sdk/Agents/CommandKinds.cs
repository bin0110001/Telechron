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
}
