using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Server.Kestrel.Https;

namespace Telechron.Host.Agents.Mtls;

// R-SEC2: Agents authenticate via mTLS. This wires Kestrel to request (and,
// on the gRPC endpoint, require) a client certificate, and registers the
// ASP.NET Core certificate-auth scheme that validates it was issued by our
// trusted dev CA (scripts/Generate-DevCerts.ps1). Human-facing API auth
// (R-SEC6, JWT bearer) is a completely separate scheme — this one is
// Agent-channel only.
public static class MtlsServiceCollectionExtensions
{
    public const string CertificateAuthScheme = CertificateAuthenticationDefaults.AuthenticationScheme;

    public static IServiceCollection AddTelechronAgentMtls(this IServiceCollection services, MtlsOptions options)
    {
        if (string.IsNullOrEmpty(options.CaCertPath) || !File.Exists(options.CaCertPath))
            throw new InvalidOperationException(
                $"mTLS CA certificate not found at '{options.CaCertPath}'. Run scripts/Generate-DevCerts.ps1 " +
                "and set TELECHRON_MTLS_CA_PATH before starting the Host.");

        var caCert = X509CertificateLoader.LoadCertificateFromFile(options.CaCertPath);

        services.AddSingleton(options);
        services.AddAuthentication(CertificateAuthScheme)
            .AddCertificate(o =>
            {
                o.AllowedCertificateTypes = CertificateTypes.All;
                o.RevocationMode = X509RevocationMode.NoCheck; // dev CA has no CRL/OCSP endpoint
                o.CustomTrustStore = [caCert];
                o.ChainTrustValidationMode = X509ChainTrustMode.CustomRootTrust;
                o.Events = new CertificateAuthenticationEvents
                {
                    OnCertificateValidated = context =>
                    {
                        context.Principal = new System.Security.Claims.ClaimsPrincipal(
                            new System.Security.Claims.ClaimsIdentity(
                                [new System.Security.Claims.Claim("cert-subject", context.ClientCertificate.Subject)],
                                CertificateAuthScheme));
                        context.Success();
                        return Task.CompletedTask;
                    },
                };
            });

        return services;
    }

    // Configures Kestrel's HTTPS transport to request AND validate a client
    // cert during the TLS handshake itself. Kestrel performs its own chain
    // check at this layer BEFORE a request ever reaches ASP.NET's auth
    // middleware — without ClientCertificateValidation overriding the
    // default (OS trust store) check, a self-signed dev CA cert fails
    // Kestrel's transport-level validation and the handshake aborts
    // silently (observed on the client as "SslStream disposed mid-write",
    // with nothing logged server-side since it never reaches app code).
    public static void ConfigureAgentMtlsTransport(this HttpsConnectionAdapterOptions https, X509Certificate2 trustedCa)
    {
        https.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
        https.ClientCertificateValidation = (cert, chain, _) =>
        {
            using var validationChain = new X509Chain();
            validationChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            validationChain.ChainPolicy.CustomTrustStore.Add(trustedCa);
            validationChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            return validationChain.Build(cert);
        };
    }
}
