namespace Telechron.Sdk;

// Telechron.Sdk holds only shared contracts/interfaces (module contracts, function
// contracts, DTOs) referenced by Host, Agent, and Modules. No leaf logic lives here (R-ENG3).
// Marker type confirming the assembly loads; real contracts land in Phase 1/2.
public static class AssemblyInfo
{
    public const string Name = "Telechron.Sdk";
}
