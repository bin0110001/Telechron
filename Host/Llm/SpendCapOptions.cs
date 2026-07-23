namespace Telechron.Host.Llm;

// R-LLM4: "Configurable rolling-window spend caps; when exceeded, new
// repair/synthesis calls are queued or declined... until window reset or
// operator override." Independent of per-repair (R-FIX3) and per-Persona
// caps -- this is the circuit breaker for a burst across many
// individually-capped pipelines.
public sealed class SpendCapOptions
{
    public TimeSpan WindowDuration { get; set; } = TimeSpan.FromHours(24);
    public decimal GlobalCapUsd { get; set; } = 50m;

    // projectId -> cap; a Project not listed here is only bound by the
    // global cap.
    public Dictionary<Guid, decimal> PerProjectCapsUsd { get; set; } = [];
}
