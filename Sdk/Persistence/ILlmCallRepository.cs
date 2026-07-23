using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Persistence;

public interface ILlmCallRepository : IRepository<LlmCall, Guid>
{
    // R-LLM4: rolling-window spend cap evaluation needs exactly this --
    // every call in a window, optionally scoped to one Project (global
    // cap passes null).
    Task<IReadOnlyList<LlmCall>> GetSinceAsync(DateTimeOffset sinceUtc, Guid? projectId = null, CancellationToken ct = default);
}
