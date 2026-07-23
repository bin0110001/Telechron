namespace Telechron.Sdk.Modules;

// R-MOD5a: what a module publisher ships alongside the assembly so the
// Host can verify it before ever loading it. Sha256Checksum guards
// against corruption/tampering-in-transit; Signature (over the checksum
// bytes) proves it came from a key the Host actually trusts -- checksum
// alone only proves "this is exactly some bytes," not "a known publisher
// produced these bytes."
public sealed record ModuleIntegrityManifest(string PublisherKeyId, string Sha256ChecksumHex, string SignatureBase64);
