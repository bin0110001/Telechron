namespace Telechron.Host.Repair;

// R-SEC3's Host-held signing identity. A single ECDSA P-256 keypair
// dedicated to repair provenance -- NOT the module-publisher trust store
// (Host/Modules/Integrity/TrustedPublisherKeyStore), which verifies
// external signatures rather than producing the Host's own. PrivateKeyPkcs8Base64
// is expected to come from a real secret store in production; test/dev
// configuration may generate an ephemeral keypair (see
// RepairProvenanceSigner's parameterless-friendly factory usage in tests).
public sealed class RepairProvenanceSignerOptions
{
    public string KeyId { get; set; } = "telechron-repair-provenance-default";
    public string? PrivateKeyPkcs8Base64 { get; set; }
    public string? PublicKeySubjectPublicKeyInfoBase64 { get; set; }
}
