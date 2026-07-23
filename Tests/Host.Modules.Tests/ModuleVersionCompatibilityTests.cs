using Telechron.Host.Modules;
using Telechron.Sdk.Modules;

namespace Telechron.Host.Modules.Tests;

public class ModuleVersionCompatibilityTests
{
    [Theory]
    [InlineData(1, 0, 0, 1, 1, 0)]
    [InlineData(1, 5, 2, 1, 5, 3)]
    [InlineData(2, 0, 0, 2, 9, 9)]
    public void Classify_SameMajorNewerMinorOrPatch_IsTransparentRebind(
        int iMajor, int iMinor, int iPatch, int cMajor, int cMinor, int cPatch)
    {
        var outcome = ModuleVersionCompatibility.Classify(
            new ModuleVersion(iMajor, iMinor, iPatch), new ModuleVersion(cMajor, cMinor, cPatch));

        Assert.Equal(ModuleVersionCompatibilityOutcome.TransparentRebind, outcome);
    }

    [Fact]
    public void Classify_DifferingMajor_RequiresReapproval()
    {
        var outcome = ModuleVersionCompatibility.Classify(new ModuleVersion(1, 9, 9), new ModuleVersion(2, 0, 0));

        Assert.Equal(ModuleVersionCompatibilityOutcome.RequiresReapproval, outcome);
    }

    [Theory]
    [InlineData(1, 0, 0, 1, 0, 0)] // identical
    [InlineData(1, 5, 0, 1, 4, 9)] // downgrade
    [InlineData(2, 0, 0, 1, 9, 9)] // major downgrade
    public void Classify_NotNewerThanInstalled_IsNotAnUpgrade(
        int iMajor, int iMinor, int iPatch, int cMajor, int cMinor, int cPatch)
    {
        var outcome = ModuleVersionCompatibility.Classify(
            new ModuleVersion(iMajor, iMinor, iPatch), new ModuleVersion(cMajor, cMinor, cPatch));

        Assert.Equal(ModuleVersionCompatibilityOutcome.NotAnUpgrade, outcome);
    }
}
