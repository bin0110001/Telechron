namespace Telechron.Sdk.Domain;

// A Project's repair autonomy (R-DM1, R-FIX3). Bounded per R-NS3 — even
// FullyAutonomous never authorizes capability synthesis or privileged-path
// changes (R-FIX10, R-SEC4); those always route to RequireApproval regardless.
public enum RepairPolicy
{
    RequireApproval = 0,
    FullyAutonomous = 1,
}
