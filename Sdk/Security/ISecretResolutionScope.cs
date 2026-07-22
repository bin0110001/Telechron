namespace Telechron.Sdk.Security;

// R-SEC5: the final-hop boundary where a raw secret is ever injected into an
// outbound call. A Persona/tool-call construction step only ever sees
// Secret.Handle strings; this scope is invoked strictly inside the
// Host/Connector runtime layer, never inside Persona logic. Tool results
// returned toward the LLM must be passed through ScrubForPromptReentry before
// they re-enter prompt context, since results commonly echo back into the
// next turn and could otherwise carry a resolved value forward.
public interface ISecretResolutionScope
{
    // Executes finalHopCall with the raw secret resolved from handle,
    // available only inside the callback. The raw value is never returned to
    // the caller of ExecuteAsync itself — only finalHopCall's result is.
    Task<TResult> ExecuteAsync<TResult>(
        string handle,
        Func<ReadOnlyMemory<byte>, Task<TResult>> finalHopCall,
        CancellationToken ct = default);

    // Re-tokenizes/redacts any resolved secret value that may have leaked into
    // a tool result before it re-enters LLM prompt context (R-SEC5).
    string ScrubForPromptReentry(string toolResult, IReadOnlyCollection<string> handlesInScope);
}
