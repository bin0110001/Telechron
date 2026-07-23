using Telechron.Sdk.Repair;

namespace Telechron.Sdk.Tests.Repair;

public class PrivilegedPathGuardTests
{
    private readonly PrivilegedPathGuard _guard = new();

    [Theory]
    [InlineData("Sdk/Domain/Persona.cs")]
    [InlineData("Host/Modules/Permissions/ModuleCapabilityMediator.cs")]
    [InlineData("Sdk/Repair/RepairPipelineOrchestrator.cs")]
    [InlineData("Host/Security/Secrets/SecretResolutionScope.cs")]
    [InlineData("Host/Repair/ApprovalGate.cs")]
    [InlineData("Host/Modules/ModuleTrustEvaluator.cs")]
    [InlineData("Sdk/Domain/DesignDocument.cs")]
    [InlineData("Sdk/Domain/Requirement.cs")]
    public void Check_PrivilegedPath_IsFlagged(string path)
    {
        var result = _guard.Check(new PatchDiff([new PatchFileChange(path, "diff")]));

        Assert.True(result.IsPrivileged);
        Assert.Contains(path, result.MatchedPaths);
    }

    [Fact]
    public void Check_OrdinarySourceFile_IsNotFlagged()
    {
        var result = _guard.Check(new PatchDiff([new PatchFileChange("Sdk/Findings/FindingsGenerator.cs", "diff")]));

        Assert.False(result.IsPrivileged);
        Assert.Empty(result.MatchedPaths);
    }

    [Fact]
    public void Check_OnePrivilegedFileAmongMany_FlagsTheWholePatch()
    {
        // R-SEC7's own note: R-FIX7 allows multi-file atomic patches, so a
        // privileged file bundled alongside innocuous ones must still be caught.
        var patch = new PatchDiff([
            new PatchFileChange("Sdk/Findings/FindingsGenerator.cs", "diff"),
            new PatchFileChange("Sdk/Domain/Persona.cs", "diff"),
        ]);

        var result = _guard.Check(patch);

        Assert.True(result.IsPrivileged);
        Assert.Single(result.MatchedPaths);
        Assert.Equal("Sdk/Domain/Persona.cs", result.MatchedPaths[0]);
    }
}
