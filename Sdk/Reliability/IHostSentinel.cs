namespace Telechron.Sdk.Reliability;

using Telechron.Sdk.Domain;
using Telechron.Sdk.Repair;

public sealed record HostSelfRepairReport
{
    public required bool RepairNeeded { get; init; }
    public required bool RequiresHumanApproval { get; init; }
    public RepairAttempt? RepairAttempt { get; init; }
    public string? Reason { get; init; }
}

public interface IHostSentinel
{
    Task<HostSelfRepairReport> RunSelfRepairCheckAsync(CancellationToken ct = default);
}
