namespace Telechron.Sdk.Modules;

// R-DM7a: semver compatibility rules -- same Major rebinds transparently,
// a differing Major requires re-approval. IComparable so callers can pick
// "latest compatible" without hand-rolling the comparison.
public readonly record struct ModuleVersion(int Major, int Minor, int Patch) : IComparable<ModuleVersion>
{
    public bool IsCompatibleWith(ModuleVersion other) => Major == other.Major;

    public int CompareTo(ModuleVersion other)
    {
        var majorCompare = Major.CompareTo(other.Major);
        if (majorCompare != 0) return majorCompare;
        var minorCompare = Minor.CompareTo(other.Minor);
        if (minorCompare != 0) return minorCompare;
        return Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}
