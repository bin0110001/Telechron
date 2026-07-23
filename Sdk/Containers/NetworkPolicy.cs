namespace Telechron.Sdk.Containers;

// R-SYS7: default-deny egress. AllowedEgressHosts is the only escape hatch,
// populated per declared Connector/Toolchain need — never a blanket allow.
// None means fully network-isolated (the default posture for anything that
// hasn't declared a specific need).
public sealed record NetworkPolicy(bool AllowNetwork, IReadOnlyList<string> AllowedEgressHosts)
{
    public static NetworkPolicy None { get; } = new(AllowNetwork: false, AllowedEgressHosts: []);
}
