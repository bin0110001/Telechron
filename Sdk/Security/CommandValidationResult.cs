namespace Telechron.Sdk.Security;

public sealed record CommandValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    public static CommandValidationResult Valid { get; } = new(true, []);
    public static CommandValidationResult Invalid(params string[] errors) => new(false, errors);
}

// R-SEC2: validates a dispatched command's kind + parameters against a
// known schema before it is ever enqueued for an Agent, and escapes any
// parameter value that will end up as a shell/process argument downstream —
// the two distinct halves of "schema validation and parameter escaping to
// prevent command injection."
public interface ICommandDispatchValidator
{
    CommandValidationResult Validate(string commandKind, string parametersJson);

    // Returns the value transformed so it is safe to place literally inside
    // a shell command line (quoting/escaping shell metacharacters) — used
    // when a validated parameter is later composed into an Agent-side
    // process invocation (Phase 4's container execution boundary).
    string EscapeForShellArgument(string value);
}
