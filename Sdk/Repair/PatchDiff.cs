namespace Telechron.Sdk.Repair;

// R-FIX7: "Repairs are atomic multi-file patch transactions." One
// PatchDiff spans every file the fix touches -- Apply/Commit treat it as
// a single unit, never per-file.
public sealed record PatchFileChange(string RelativePath, string UnifiedDiff);

public sealed record PatchDiff(IReadOnlyList<PatchFileChange> FileChanges)
{
    public IReadOnlyList<string> TouchedFilePaths => FileChanges.Select(f => f.RelativePath).ToList();
}
