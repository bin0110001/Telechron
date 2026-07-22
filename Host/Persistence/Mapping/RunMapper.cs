using Telechron.Host.Persistence.Entities;
using Telechron.Sdk.Domain;

namespace Telechron.Host.Persistence.Mapping;

public static class RunMapper
{
    public static Run ToDomain(this RunEntity entity) => new()
    {
        Id = entity.Id,
        ProjectId = entity.ProjectId,
        MachineId = entity.MachineId,
        Status = (RunStatus)entity.Status,
        StartedAtUtc = entity.StartedAtUtc,
        CompletedAtUtc = entity.CompletedAtUtc,
        LastHeartbeatUtc = entity.LastHeartbeatUtc,
        SuiteResultsJson = entity.SuiteResultsJson,
        LogsRef = entity.LogsRef,
    };

    public static RunEntity ToEntity(this Run domain) => new()
    {
        Id = domain.Id,
        ProjectId = domain.ProjectId,
        MachineId = domain.MachineId,
        Status = (int)domain.Status,
        StartedAtUtc = domain.StartedAtUtc,
        CompletedAtUtc = domain.CompletedAtUtc,
        LastHeartbeatUtc = domain.LastHeartbeatUtc,
        SuiteResultsJson = domain.SuiteResultsJson,
        LogsRef = domain.LogsRef,
    };

    public static void ApplyTo(this Run domain, RunEntity entity)
    {
        entity.MachineId = domain.MachineId;
        entity.Status = (int)domain.Status;
        entity.StartedAtUtc = domain.StartedAtUtc;
        entity.CompletedAtUtc = domain.CompletedAtUtc;
        entity.LastHeartbeatUtc = domain.LastHeartbeatUtc;
        entity.SuiteResultsJson = domain.SuiteResultsJson;
        entity.LogsRef = domain.LogsRef;
    }
}
