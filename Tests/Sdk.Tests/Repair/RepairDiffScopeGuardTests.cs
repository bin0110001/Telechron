using Telechron.Sdk.Repair;

namespace Telechron.Sdk.Tests.Repair;

public class RepairDiffScopeGuardTests
{
    private readonly RepairDiffScopeGuard _guard = new(new RepairDiffScopeOptions(MaxFileCount: 3, MaxTotalLineCount: 20));

    [Fact]
    public void Check_SmallInOriginPatch_DoesNotExceedScope()
    {
        var patch = new PatchDiff([new PatchFileChange("Foo.cs", "--- a/Foo.cs\n+++ b/Foo.cs\n@@ -1 +1 @@\n-old\n+new\n")]);

        var result = _guard.Check(patch, ["Foo.cs"]);

        Assert.False(result.ExceedsScope);
    }

    [Fact]
    public void Check_TooManyFiles_ExceedsScope()
    {
        var patch = new PatchDiff([
            new PatchFileChange("A.cs", "+a"), new PatchFileChange("B.cs", "+b"),
            new PatchFileChange("C.cs", "+c"), new PatchFileChange("D.cs", "+d"),
        ]);

        var result = _guard.Check(patch, []);

        Assert.True(result.ExceedsScope);
        Assert.Contains("4 files", result.Reason);
    }

    [Fact]
    public void Check_TooManyChangedLines_ExceedsScope()
    {
        var manyLines = string.Join("\n", Enumerable.Range(0, 25).Select(i => $"+line {i}"));
        var patch = new PatchDiff([new PatchFileChange("Foo.cs", manyLines)]);

        var result = _guard.Check(patch, []);

        Assert.True(result.ExceedsScope);
        Assert.Contains("lines", result.Reason);
    }

    [Fact]
    public void Check_PatchTouchesFileOutsideDeclaredOrigin_ExceedsScope()
    {
        var patch = new PatchDiff([
            new PatchFileChange("Foo.cs", "+change"),
            new PatchFileChange("Unrelated.cs", "+sneaky change"),
        ]);

        var result = _guard.Check(patch, ["Foo.cs"]);

        Assert.True(result.ExceedsScope);
        Assert.Contains("Unrelated.cs", result.Reason);
    }

    [Fact]
    public void Check_NoDeclaredOriginPaths_SkipsOriginCheck()
    {
        // An empty declared-origin list means "no origin info available"
        // (e.g. a test-failure Finding with no OriginFilePath) -- must not
        // be interpreted as "nothing is in scope."
        var patch = new PatchDiff([new PatchFileChange("Foo.cs", "+change")]);

        var result = _guard.Check(patch, []);

        Assert.False(result.ExceedsScope);
    }
}
