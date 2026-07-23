using Telechron.Sdk.Domain;
using Telechron.Sdk.Modules.Runners;
using Telechron.Sdk.Repair;

namespace Telechron.Host.Repair.Tests.Fixtures;

// Controllable stand-ins for the two seams that genuinely need external
// infrastructure (a real LLM, a real container runtime) to exercise for
// real -- covered separately by live tests. Everything else in these
// orchestrator tests (git, SQLite, the gate evaluators, the governor,
// the concurrency gate, the provenance signer) is the REAL implementation.
public sealed class FakeLlmFixGenerator(Func<LlmFixContext, LlmFixResult> respond) : ILlmFixGenerator
{
    public int CallCount { get; private set; }

    public Task<LlmFixResult> GenerateAsync(LlmFixContext context, CancellationToken ct = default)
    {
        CallCount++;
        return Task.FromResult(respond(context));
    }

    public static FakeLlmFixGenerator ProducingPatch(PatchDiff patch) =>
        new(_ => new LlmFixResult(true, patch, false, "fake response"));

    public static FakeLlmFixGenerator RequiringSynthesis() =>
        new(_ => new LlmFixResult(true, null, true, "REQUIRES_CAPABILITY_SYNTHESIS"));

    public static FakeLlmFixGenerator Declining() =>
        new(_ => new LlmFixResult(false, null, false, "declined"));
}

public sealed class FakeRepairVerifier(Func<VerifyResult> respond) : IRepairVerifier
{
    public int CallCount { get; private set; }

    public Task<VerifyResult> VerifyAsync(string projectRootPath, CancellationToken ct = default)
    {
        CallCount++;
        return Task.FromResult(respond());
    }

    public static FakeRepairVerifier Succeeding() => new(() =>
        new VerifyResult(true, new TestRunResult(true, [], [], "all passed"), "all passed"));

    public static FakeRepairVerifier Failing() => new(() =>
        new VerifyResult(false, new TestRunResult(false, [new TestCaseResult("Test1", TestOutcome.Failed, "still broken")], [], "1 failed"), "1 failed"));
}

public sealed class FakeArchitecturalDriftDetector(bool isDrift = false) : IArchitecturalDriftDetector
{
    public Task<DriftCheckResult> CheckAsync(PatchDiff patch, IReadOnlyList<Requirement> activeRequirements, CancellationToken ct = default) =>
        Task.FromResult(isDrift
            ? new DriftCheckResult(true, "fake drift reason")
            : new DriftCheckResult(false, null));
}
