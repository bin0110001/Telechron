using System.Collections.Concurrent;

namespace Telechron.Host.Modules.Runtime;

// R-MOD6a phase 1: tracks how many calls are currently in flight against a
// given module version, and whether new dispatch is currently accepted.
// TryBeginInvocation/EndInvocation bracket every call into a loaded
// module -- this is what lets drain know when in-flight work has actually
// drained to zero, rather than guessing from a timer alone.
public sealed class InFlightInvocationTracker
{
    private sealed class ModuleState
    {
        public int InFlightCount;
        public bool AcceptingNewDispatch = true;
    }

    private readonly ConcurrentDictionary<string, ModuleState> _byModuleName = new();

    public bool TryBeginInvocation(string moduleName)
    {
        var state = _byModuleName.GetOrAdd(moduleName, static _ => new ModuleState());
        lock (state)
        {
            if (!state.AcceptingNewDispatch)
                return false;
            state.InFlightCount++;
            return true;
        }
    }

    public void EndInvocation(string moduleName)
    {
        if (_byModuleName.TryGetValue(moduleName, out var state))
        {
            lock (state)
            {
                state.InFlightCount = Math.Max(0, state.InFlightCount - 1);
            }
        }
    }

    // R-MOD6a phase 1 start: "stops dispatching new invocations to the
    // outgoing version." Existing in-flight calls are unaffected; they
    // must complete (or hit the bounded timeout) on their own.
    public void StopAcceptingNewDispatch(string moduleName)
    {
        var state = _byModuleName.GetOrAdd(moduleName, static _ => new ModuleState());
        lock (state)
        {
            state.AcceptingNewDispatch = false;
        }
    }

    public int GetInFlightCount(string moduleName) =>
        _byModuleName.TryGetValue(moduleName, out var state) ? state.InFlightCount : 0;

    public void Reset(string moduleName) => _byModuleName.TryRemove(moduleName, out _);
}
