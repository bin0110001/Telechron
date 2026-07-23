using Telechron.Sdk.Modules;

namespace Telechron.Modules.Sample;

// R-MOD1/R-MOD3: a minimal but complete IModule implementation proving the
// whole module contract end to end -- source (this file), a real self-test
// with a genuine negative control (R-MOD4a), and a declared capability
// (R-MOD8). Deliberately trivial in what it *does* (adds two numbers) so
// the self-test's pass/fail behavior is the interesting part, not the
// module logic itself.
public sealed class SampleModule : IModule
{
    public string Name => "telechron.sample";
    public string Kind => "sample";
    public ModuleVersion Version => new(1, 0, 0);

    // R-MOD8: this module only ever computes in-process -- no filesystem,
    // network, or process-execution capability is needed, so none is
    // declared. Under-declared capability *use* (not declaration) is what
    // R-MOD5b's pre-trust sandbox catches; this module simply has nothing
    // to under-declare.
    public IReadOnlyList<string> DeclaredCapabilities => [];

    public Task<ModuleSelfTestResult> RunSelfTestAsync(CancellationToken ct = default)
    {
        // A genuine negative control (R-MOD4a): Add(2, 2) must be 4, not
        // "always true". Introducing a real bug (e.g. `a - b`) makes this
        // self-test fail, which is exactly what falsifiability checking
        // verifies -- see ISelfTestFalsifiabilityChecker.
        var result = Add(2, 2);
        return Task.FromResult(result == 4
            ? ModuleSelfTestResult.Success($"Add(2, 2) == {result}, as expected.")
            : ModuleSelfTestResult.Failure("Add(2, 2) did not equal 4.", $"Actual: {result}"));
    }

    public static int Add(int a, int b) => a + b;
}
