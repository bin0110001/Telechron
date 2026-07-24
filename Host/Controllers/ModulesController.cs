using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telechron.Host.Security.Auth;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Controllers;

public sealed record ModuleResponse(
    Guid Id, string Name, string Kind, string Version, IReadOnlyList<string> Capabilities, DateTimeOffset InstalledAtUtc);

// R-UI2/R-MOD1: installed Modules are Host-wide (not Project-scoped).
[ApiController]
[Route("api/modules")]
[Authorize(Policy = AuthServiceCollectionExtensions.Policies.RequireViewer)]
public sealed class ModulesController(IModuleRepository moduleRepository) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ModuleResponse>>> ListAsync(CancellationToken ct)
    {
        var modules = await moduleRepository.GetAllAsync(ct);
        return Ok(modules.Select(ToResponse).ToList());
    }

    [HttpGet("{moduleId:guid}")]
    public async Task<ActionResult<ModuleResponse>> GetAsync(Guid moduleId, CancellationToken ct)
    {
        var module = await moduleRepository.GetByIdAsync(moduleId, ct);
        return module is null ? NotFound() : Ok(ToResponse(module));
    }

    private static ModuleResponse ToResponse(Module m)
    {
        IReadOnlyList<string> capabilities;
        try
        {
            capabilities = Telechron.Sdk.Modules.ModuleCapabilities.Parse(m.CapabilitiesJson)
                .Select(c => c.Kind.ToString())
                .ToList();
        }
        catch (System.Text.Json.JsonException)
        {
            capabilities = [];
        }

        return new ModuleResponse(m.Id, m.Name, m.Kind, $"{m.VersionMajor}.{m.VersionMinor}.{m.VersionPatch}", capabilities, m.InstalledAtUtc);
    }
}
