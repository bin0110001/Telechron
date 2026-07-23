using System.Security.Cryptography.X509Certificates;
using global::Grpc.Core;

namespace Telechron.Host.Agents.Tests.Fixtures;

// Minimal ServerCallContext test double — AgentServiceImpl only reads
// CancellationToken from the context, so every other member is a
// throw-if-touched placeholder that would fail a test loudly if a future
// change starts depending on it.
public sealed class TestServerCallContext(CancellationToken ct) : ServerCallContext
{
    protected override CancellationToken CancellationTokenCore => ct;
    protected override string MethodCore => "test-method";
    protected override string HostCore => "test-host";
    protected override string PeerCore => "test-peer";
    protected override DateTime DeadlineCore => DateTime.MaxValue;
    protected override Metadata RequestHeadersCore => [];
    protected override Metadata ResponseTrailersCore => [];
    protected override Status StatusCore { get; set; }
    protected override WriteOptions? WriteOptionsCore { get; set; }
    protected override AuthContext AuthContextCore => throw new NotSupportedException();
    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
        throw new NotSupportedException();
    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
}
