using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Persistence;

public interface IIntentPlanRepository : IRepository<IntentPlan, Guid>
{
    Task<IReadOnlyList<IntentPlan>> GetByProjectAsync(Guid projectId, CancellationToken ct = default);
}
