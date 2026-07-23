namespace Telechron.Host.Agents;

// R-SEC2: the enrollment token authorizes a Machine to register at all,
// distinct from the mTLS client cert that authenticates the transport
// connection itself — both must be presented. A single shared token is a
// placeholder for this phase; per-Agent one-time enrollment tokens
// (issued via the human-facing API, consumed on first registration) are the
// natural follow-up once R-UI2's Machines surface exists (Phase 10).
public sealed class AgentEnrollmentOptions
{
    public string EnrollmentToken { get; set; } = string.Empty;
}
