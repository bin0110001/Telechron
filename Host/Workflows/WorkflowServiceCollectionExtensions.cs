using Telechron.Host.Workflows.Approvals;
using Telechron.Sdk.Workflows;
using Telechron.Sdk.Workflows.Approvals;

namespace Telechron.Host.Workflows;

public static class WorkflowServiceCollectionExtensions
{
    // R-WF5/R-DM15: IApprovalManager has zero dependency on anything
    // Agent/mTLS-related (pure in-memory approval-request bookkeeping) --
    // registered separately and unconditionally so the human approval
    // surface (e.g. ApprovalsController) works even when no Agent
    // transport is configured, unlike IWorkflowEngine below which
    // genuinely needs IModuleRuntime.
    public static IServiceCollection AddTelechronApprovals(this IServiceCollection services)
    {
        services.AddSingleton<IApprovalManager, ApprovalManager>();
        return services;
    }

    // R-WF1/R-WF4: IWorkflowEngine and WorkflowRecoveryService (the
    // durable-resume-across-restart hosted service) were previously never
    // registered anywhere the running Host would construct them -- both
    // existed only as classes exercised by their own unit tests, same
    // orphaning bug as Phase 9's scheduler/sentinel. Requires
    // AddTelechronApprovals (above) and AddTelechronModules (IModuleRuntime)
    // to already be registered.
    public static IServiceCollection AddTelechronWorkflows(this IServiceCollection services)
    {
        services.AddScoped<IWorkflowEngine, WorkflowEngine>();
        services.AddHostedService<WorkflowRecoveryService>();
        return services;
    }
}
