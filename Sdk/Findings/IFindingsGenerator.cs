using Telechron.Sdk.Domain;

namespace Telechron.Sdk.Findings;

// R-FIX1: "Runs/workflow failures produce Findings." One Finding per
// distinct failure signal -- a Run with three failing test cases produces
// three Findings, not one, so each can be repaired (or classified/capped)
// independently. FailureClass is set via IFailureClassifier -- this
// generator never re-derives environment-vs-code itself, per R-ENG4.
public interface IFindingsGenerator
{
    IReadOnlyList<Finding> GenerateFromRun(Run run, FailureClassificationInput classificationInput, CancellationToken ct = default);

    // R-ENG1: file-length lint violations are always Code-classified --
    // there's no "environment" reading of a file being too long, so this
    // doesn't route through IFailureClassifier at all.
    IReadOnlyList<Finding> GenerateFromFileLengthLint(Guid projectId, string repoRoot, CancellationToken ct = default);
}
