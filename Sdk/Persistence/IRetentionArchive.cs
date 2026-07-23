namespace Telechron.Sdk.Persistence;

// R-PER7: "archival to cold storage before deletion" — the write side of a
// retention pass. AppendAsync is called once per row about to be pruned, so
// a crash mid-pass leaves the archive a strict prefix of what's been deleted
// (never the reverse — see RetentionPass, which archives before it deletes).
public interface IRetentionArchive
{
    Task AppendAsync(string entityTypeName, string rowJson, CancellationToken ct = default);
}
