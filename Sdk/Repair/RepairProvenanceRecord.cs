namespace Telechron.Sdk.Repair;

// R-SEC3: "Every auto-committed or hot-reloaded patch carries a signed,
// immutable provenance record linking the commit to its source
// Finding(s), generating Persona, LLM connection/model version, and
// Verify results." Stored independently of the artifact it describes
// (ProvenanceRecordJson on RepairAttempt is the serialized form of this
// record plus its signature -- the record's own fields never get
// overwritten in place, only ever a whole new RepairAttempt row).
public sealed record RepairProvenanceRecord(
    Guid RepairAttemptId,
    IReadOnlyList<Guid> FindingIds,
    string CommitReference,
    string? GeneratingPersonaId,
    string? LlmConnectionId,
    string? LlmModelUsed,
    bool VerifySucceeded,
    string VerifySummary,
    DateTimeOffset SignedAtUtc);

public sealed record SignedRepairProvenance(RepairProvenanceRecord Record, string SignatureBase64, string SignerKeyId);

// R-SEC3's signing seam. Deliberately separate from module-integrity
// signing (Host/Modules/Integrity/ModuleIntegrityVerifier) -- that
// verifies THIRD-PARTY publisher signatures on external module code;
// this is Telechron signing its OWN provenance record with a Host-held
// key so the record is tamper-evident after the fact, not proving
// external provenance.
public interface IRepairProvenanceSigner
{
    SignedRepairProvenance Sign(RepairProvenanceRecord record);

    bool Verify(SignedRepairProvenance signed);
}
