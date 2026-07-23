namespace Telechron.Sdk.Containers;

// R-SYS10: pools are keyed by (Toolchain image, project dependency
// fingerprint) -- two requests only share a warm container if both the
// Toolchain and the dependency set match exactly. DependencyFingerprint is
// caller-supplied (e.g. a hash of the lockfile/package manifest) so the
// pool never has to understand any particular ecosystem's dependency
// format.
public sealed record WarmPoolKey(string ImageDigest, string DependencyFingerprint);
