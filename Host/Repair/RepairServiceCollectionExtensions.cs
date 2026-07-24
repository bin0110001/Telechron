using Microsoft.Extensions.Options;
using Telechron.Sdk.Repair;

namespace Telechron.Host.Repair;

public static class RepairServiceCollectionExtensions
{
    // R-NS2/R-FIX2/R-ENG4: registers the Project-independent repair gates
    // (governor, concurrency, guards, provenance signer, version control)
    // plus IRepairPipelineFactory, the one seam that resolves a Project's
    // real Toolchain/TestRunner/LlmConnection and builds a working
    // RepairPipelineOrchestrator for it. Neither RepairPipelineOrchestrator
    // itself nor any of its dependents were previously registered anywhere
    // -- the whole pipeline only ever ran inside its own tests.
    public static IServiceCollection AddTelechronRepair(
        this IServiceCollection services, Action<RepairProvenanceSignerOptions>? configureProvenance = null)
    {
        services.Configure<RepairProvenanceSignerOptions>(configureProvenance ?? (_ => { }));

        services.AddSingleton<IRepairVersionControl, GitRepairVersionControl>();
        services.AddScoped<IRepairAttemptGovernor, RepairAttemptGovernor>();
        services.AddSingleton<IRepairConcurrencyGate, RepairConcurrencyGate>();
        services.AddSingleton<IDeterministicFixProvider, CompositeDeterministicFixProvider>();
        services.AddSingleton<IPrivilegedPathGuard, PrivilegedPathGuard>();
        services.AddSingleton<IRepairDiffScopeGuard, RepairDiffScopeGuard>();
        services.AddSingleton<IOscillationDetector, OscillationDetector>();
        services.AddSingleton<IRepairProvenanceSigner, RepairProvenanceSigner>();
        services.AddScoped<IRepairPipelineFactory, RepairPipelineFactory>();

        return services;
    }
}
