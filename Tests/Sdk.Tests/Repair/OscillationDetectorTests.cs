using Telechron.Sdk.Repair;

namespace Telechron.Sdk.Tests.Repair;

public class OscillationDetectorTests
{
    private readonly OscillationDetector _detector = new();

    [Fact]
    public void Check_NoMatchingPriorSignature_IsNotOscillation()
    {
        var patch = new PatchDiff([new PatchFileChange("Foo.cs", "--- a/Foo.cs\n+++ b/Foo.cs\n@@ -1 +1 @@\n-old\n+new\n")]);

        var result = _detector.Check(patch, ["some-other-signature"]);

        Assert.False(result.IsOscillation);
    }

    [Fact]
    public void Check_MatchingPriorSignature_IsOscillation()
    {
        var patch = new PatchDiff([new PatchFileChange("Foo.cs", "--- a/Foo.cs\n+++ b/Foo.cs\n@@ -1 +1 @@\n-old\n+new\n")]);
        var signature = _detector.ComputeSignature(patch);

        var result = _detector.Check(patch, [signature]);

        Assert.True(result.IsOscillation);
    }

    [Fact]
    public void ComputeSignature_IdenticalContentDifferentHunkHeaders_ProducesSameSignature()
    {
        // Hunk line numbers shift trivially between otherwise-identical
        // patches (e.g. an earlier unrelated line added/removed) -- the
        // signature must be stable to that, or oscillation detection would
        // miss the A-B-A-B case whenever surrounding context drifts.
        var patchA = new PatchDiff([new PatchFileChange("Foo.cs", "@@ -1,3 +1,3 @@\n-old\n+new\n")]);
        var patchB = new PatchDiff([new PatchFileChange("Foo.cs", "@@ -10,3 +10,3 @@\n-old\n+new\n")]);

        Assert.Equal(_detector.ComputeSignature(patchA), _detector.ComputeSignature(patchB));
    }

    [Fact]
    public void ComputeSignature_DifferentContent_ProducesDifferentSignature()
    {
        var patchA = new PatchDiff([new PatchFileChange("Foo.cs", "@@ -1,3 +1,3 @@\n-old\n+new\n")]);
        var patchB = new PatchDiff([new PatchFileChange("Foo.cs", "@@ -1,3 +1,3 @@\n-old\n+completely different\n")]);

        Assert.NotEqual(_detector.ComputeSignature(patchA), _detector.ComputeSignature(patchB));
    }

    [Fact]
    public void ComputeSignature_FileOrderIndependent_ProducesSameSignature()
    {
        var patchA = new PatchDiff([
            new PatchFileChange("A.cs", "@@ -1 +1 @@\n-old-a\n+new-a\n"),
            new PatchFileChange("B.cs", "@@ -1 +1 @@\n-old-b\n+new-b\n"),
        ]);
        var patchB = new PatchDiff([
            new PatchFileChange("B.cs", "@@ -1 +1 @@\n-old-b\n+new-b\n"),
            new PatchFileChange("A.cs", "@@ -1 +1 @@\n-old-a\n+new-a\n"),
        ]);

        Assert.Equal(_detector.ComputeSignature(patchA), _detector.ComputeSignature(patchB));
    }
}
