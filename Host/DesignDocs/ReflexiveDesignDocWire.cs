namespace Telechron.Host.DesignDocs;

using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

public sealed class ReflexiveDesignDocWire(IDesignDocumentRepository designDocRepo)
{
    public static readonly Guid TelechronSelfProjectId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public async Task<DesignDocument?> GetTelechronSelfDesignDocumentAsync(CancellationToken ct = default)
    {
        return await designDocRepo.GetByProjectAsync(TelechronSelfProjectId, ct);
    }
}
