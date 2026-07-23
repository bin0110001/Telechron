namespace Telechron.Sdk.Containers;

public sealed record ImageProvenanceResult(bool IsAllowed, string Reason);

// R-SYS9: images pinned by digest, from an allowlisted registry. This check
// runs before ANY container is created — ContainerExecutionService refuses
// to proceed if it fails, regardless of caller.
public interface IImageProvenanceVerifier
{
    ImageProvenanceResult Verify(string imageReference);
}
