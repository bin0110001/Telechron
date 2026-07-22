namespace Telechron.Sdk.Tests;

public class SolutionSkeletonTests
{
    [Fact]
    public void SdkAssembly_IsLoadable()
    {
        Assert.NotNull(typeof(Telechron.Sdk.AssemblyInfo));
    }
}
