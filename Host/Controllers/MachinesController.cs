using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telechron.Host.Security.Auth;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Controllers;

public sealed record MachineResponse(
    Guid Id, string Name, string Hostname, bool IsOnline, DateTimeOffset RegisteredAtUtc, DateTimeOffset? LastHeartbeatUtc);

// R-UI2: Machines are a Host-wide resource (not Project-scoped), so this
// surface is visible to any authenticated user, same as the Diagnostics
// baseline -- there is no per-Machine ownership model to check against.
[ApiController]
[Route("api/machines")]
[Authorize(Policy = AuthServiceCollectionExtensions.Policies.RequireViewer)]
public sealed class MachinesController(IMachineRepository machineRepository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MachineResponse>>> ListAsync(CancellationToken ct)
    {
        var machines = await machineRepository.GetAllAsync(ct);
        return Ok(machines.Select(ToResponse).ToList());
    }

    [HttpGet("{machineId:guid}")]
    public async Task<ActionResult<MachineResponse>> GetAsync(Guid machineId, CancellationToken ct)
    {
        var machine = await machineRepository.GetByIdAsync(machineId, ct);
        return machine is null ? NotFound() : Ok(ToResponse(machine));
    }

    private static MachineResponse ToResponse(Machine m) =>
        new(m.Id, m.Name, m.Hostname, m.IsOnline, m.RegisteredAtUtc, m.LastHeartbeatUtc);
}
