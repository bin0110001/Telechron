namespace Telechron.Sdk.Security.Audit;

// R-SEC7: the security-relevant action classes explicitly called out as
// required audit content. Extend deliberately — this enum is the taxonomy the
// tamper-evident log is queried/filtered by.
public enum AuditEventKind
{
    SecretAccessed = 0,
    SecretCreated = 1,
    SecretRotated = 2,
    SecretRevoked = 3,
    ApprovalDecision = 4,
    ModuleInstalled = 5,
    CapabilityGranted = 6,
    RepairAutoCommitted = 7,
    AuthenticationFailed = 8,
    AuthorizationDenied = 9,
}
