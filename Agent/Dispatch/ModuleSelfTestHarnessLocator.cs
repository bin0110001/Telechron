using Microsoft.Extensions.Options;

namespace Telechron.Agent.Dispatch;

// The self-test harness (Tools/ModuleSelfTestHarness) is deployed
// alongside the Agent itself -- same publish/deploy artifact, since it's
// Telechron-owned infrastructure, not module-supplied content. This
// locates that pre-published output and stages a copy into each self-test
// workspace so it's visible to the container via the same bind mount as
// the module assembly (the container has no access to the Agent's own
// filesystem beyond what's explicitly bind-mounted, per R-SYS7).
public sealed class ModuleSelfTestHarnessLocator(IOptions<ModuleSelfTestHarnessOptions> options)
{
    public void CopyHarnessInto(string workspaceDir)
    {
        var harnessDir = Path.Combine(workspaceDir, "harness");
        Directory.CreateDirectory(harnessDir);

        var sourceDir = options.Value.HarnessPublishDirectory;
        if (!Directory.Exists(sourceDir))
        {
            throw new InvalidOperationException(
                $"Module self-test harness publish output not found at '{sourceDir}'. " +
                "Set Telechron:ModuleSelfTestHarnessPublishDirectory to the harness's published output directory.");
        }

        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(harnessDir, Path.GetFileName(file)), overwrite: true);
        }
    }
}

public sealed class ModuleSelfTestHarnessOptions
{
    public string HarnessPublishDirectory { get; set; } = string.Empty;
}
