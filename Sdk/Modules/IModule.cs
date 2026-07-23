namespace Telechron.Sdk.Modules;

// R-SYS4/R-MOD3: the contract every hot-reloadable plugin implements.
// Modules are never a compile-time dependency of the Host — this interface
// is the only compile-time coupling, and it lives in Sdk (which Modules
// already reference) so a module project needs no reference to Host at all.
public interface IModule
{
    string Name { get; }
    string Kind { get; }
    ModuleVersion Version { get; }

    // R-MOD8: capabilities this module requires to function. Declared, not
    // requested-on-demand -- the whole point is the Project approves these
    // up front (R-MOD5b compares observed behavior against exactly this
    // declared set).
    IReadOnlyList<string> DeclaredCapabilities { get; }

    // R-MOD4: the unified self-test contract. Runs inside the module's own
    // execution container (R-SYS6) -- never assume in-process safety.
    Task<ModuleSelfTestResult> RunSelfTestAsync(CancellationToken ct = default);
}
