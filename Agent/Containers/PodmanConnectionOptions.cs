namespace Telechron.Agent.Containers;

public sealed class PodmanConnectionOptions
{
    // Docker.DotNet endpoint URI. Windows named pipe by default (matches
    // `podman machine inspect`'s ConnectionInfo.PodmanPipe); a Linux Agent
    // would instead point this at the Unix socket
    // (unix:///run/podman/podman.sock or the rootless user equivalent).
    public string Endpoint { get; set; } = "npipe://./pipe/podman-machine-default";
}
