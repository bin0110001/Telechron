using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using global::Grpc.Net.Client;
using Microsoft.Extensions.Options;

namespace Telechron.Agent.Grpc;

// R-SEC2: builds the mTLS-authenticated HttpClient/GrpcChannel the Agent
// uses for every call to the Host. Uses SocketsHttpHandler with explicit
// SslClientAuthenticationOptions rather than HttpClientHandler.ClientCertificates
// — the latter's client-cert attachment does not reliably complete the TLS
// handshake for HTTP/2 connections on all platforms (observed: handshake
// silently aborts with the SslStream disposed mid-write). The CA cert pins
// which Host the Agent will trust, so a compromised DNS/network can't MITM
// the connection to a rogue Host.
public sealed class AgentChannelFactory(IOptions<AgentGrpcOptions> options)
{
    public GrpcChannel CreateChannel()
    {
        var opts = options.Value;
        var handler = new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions
            {
                EnabledSslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12,
            },
        };

        if (!string.IsNullOrEmpty(opts.ClientCertPfxPath))
        {
            var clientCert = X509CertificateLoader.LoadPkcs12FromFile(opts.ClientCertPfxPath, opts.ClientCertPassword);
            handler.SslOptions.ClientCertificates = [clientCert];
            handler.SslOptions.LocalCertificateSelectionCallback = (_, _, _, _, _) => clientCert;
        }

        if (!string.IsNullOrEmpty(opts.CaCertPath))
        {
            var caCert = X509CertificateLoader.LoadCertificateFromFile(opts.CaCertPath);
            handler.SslOptions.RemoteCertificateValidationCallback = (_, cert, chain, _) =>
            {
                if (cert is null || chain is null) return false;
                chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
                chain.ChainPolicy.CustomTrustStore.Add(caCert);
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
                return chain.Build(X509CertificateLoader.LoadCertificate(cert.GetRawCertData()));
            };
        }

        return GrpcChannel.ForAddress(opts.HostAddress, new GrpcChannelOptions { HttpHandler = handler });
    }
}
