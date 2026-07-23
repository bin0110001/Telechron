namespace Telechron.Host.Agents.Dispatch;

// R-SYS2's dispatched-work examples ("Run tests, Build projects, Execute
// workflows, Repair code, Manage GPU resources") given stable string IDs.
// Extend deliberately — CommandDispatchValidator's schema registry is keyed
// by these.
public static class CommandKinds
{
    public const string RunTests = "run-tests";
    public const string Build = "build";
    public const string ExecuteWorkflowStep = "execute-workflow-step";
    public const string ApplyRepairPatch = "apply-repair-patch";
}
