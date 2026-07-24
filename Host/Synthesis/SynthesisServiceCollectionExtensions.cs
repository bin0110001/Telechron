using Microsoft.Extensions.Options;
using Telechron.Host.Intent;
using Telechron.Sdk.Intent;
using Telechron.Sdk.Synthesis;

namespace Telechron.Host.Synthesis;

public static class SynthesisServiceCollectionExtensions
{
    // R-BUILD1-5: intent planning + capability gap synthesis were
    // previously never registered anywhere the running Host would
    // construct them -- both existed only as classes exercised by their
    // own unit tests, same orphaning bug as every other Phase 7/8/9
    // subsystem this pass fixed.
    public static IServiceCollection AddTelechronSynthesis(
        this IServiceCollection services, Action<SynthesisIntegritySignerOptions>? configureIntegritySigner = null)
    {
        services.Configure<SynthesisIntegritySignerOptions>(configureIntegritySigner ?? (_ => { }));
        services.AddSingleton<SynthesisIntegritySigner>();

        services.AddScoped<ICapabilityGapAnalyzer, CapabilityGapAnalyzer>();
        services.AddScoped<DeterministicIntentPlanner>();
        services.AddScoped<LlmIntentPlanner>();
        services.AddScoped<IIntentPlanner, IntentPlanner>();

        // ICapabilitySynthesizer is NOT registered here -- CapabilitySynthesizer
        // needs a Project-specific LlmConnection (same reasoning as
        // LlmFixGenerator/ArchitecturalDriftDetector in Host/Repair/), so
        // CapabilityGapApprovalFlow resolves the connection and constructs
        // it per-call instead of taking it as a fixed DI dependency.
        services.AddScoped<ICapabilityVerificationRunner, CapabilityVerificationRunner>();
        services.AddScoped<CapabilityGapApprovalFlow>();

        return services;
    }
}
