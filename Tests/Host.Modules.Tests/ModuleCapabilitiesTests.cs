using Telechron.Sdk.Modules;
using Telechron.Sdk.Security.Permissions;

namespace Telechron.Host.Modules.Tests;

public class ModuleCapabilitiesTests
{
    [Fact]
    public void Parse_EmptyString_ReturnsEmpty()
    {
        Assert.Empty(ModuleCapabilities.Parse(""));
    }

    [Fact]
    public void Parse_ValidJson_ReturnsGrants()
    {
        var json = ModuleCapabilities.Serialize([
            new CapabilityGrant(CapabilityKind.FilesystemRead, null),
            new CapabilityGrant(CapabilityKind.SecretAccess, "my-secret-handle"),
        ]);

        var grants = ModuleCapabilities.Parse(json);

        Assert.Equal(2, grants.Count);
        Assert.Contains(grants, g => g.Kind == CapabilityKind.FilesystemRead && g.ResourceId is null);
        Assert.Contains(grants, g => g.Kind == CapabilityKind.SecretAccess && g.ResourceId == "my-secret-handle");
    }

    [Fact]
    public void Parse_UnknownCapabilityKind_IsSkippedNotThrown()
    {
        var grants = ModuleCapabilities.Parse("""[{"Kind":"NotARealCapability","ResourceId":null}]""");

        Assert.Empty(grants);
    }

    [Fact]
    public void RoundTrip_SerializeThenParse_PreservesGrants()
    {
        IReadOnlyList<CapabilityGrant> original = [
            new CapabilityGrant(CapabilityKind.GpuAccess, null),
            new CapabilityGrant(CapabilityKind.ConnectorAccess, "github"),
        ];

        var roundTripped = ModuleCapabilities.Parse(ModuleCapabilities.Serialize(original));

        Assert.Equal(original, roundTripped);
    }
}
