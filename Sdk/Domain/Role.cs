namespace Telechron.Sdk.Domain;

// Per-Project RBAC role (R-DM15, R-SEC6). Ordered least→most privileged;
// Operator can approve (Operator-Approver in R-SEC6 terms).
public enum Role
{
    Viewer = 0,
    Operator = 1,
    Admin = 2,
}
