namespace Telechron.Host.Llm;

public sealed record SpendCapCheckResult(bool IsAllowed, string Reason, decimal CurrentSpendUsd, decimal CapUsd);

// R-LLM4: checked BEFORE a call is dispatched -- the whole point is to
// decline (or, per Project Repair Policy, queue) new calls once a
// rolling-window spend cap is exceeded, not to let a call through and
// notice after the fact.
public interface ISpendCapEnforcer
{
    Task<SpendCapCheckResult> CheckAsync(Guid? projectId, CancellationToken ct = default);
}
