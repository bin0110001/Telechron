using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Telechron.Host.Security.Auth;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.Controllers;

// R-SEC1: intentionally carries no EncryptedValue/EncryptionKeyId --
// Personas/prompts only ever see opaque handles (R-SEC1), and this human
// UI surface has exactly the same restriction. There is no endpoint
// anywhere on this controller that can resolve a handle to its raw value.
public sealed record SecretResponse(Guid Id, Guid ProjectId, string Handle, string Name, DateTimeOffset CreatedAtUtc, DateTimeOffset? RevokedAtUtc);

[ApiController]
[Route("api/projects/{projectId:guid}/secrets")]
[Authorize(Policy = AuthServiceCollectionExtensions.Policies.RequireViewer)]
public sealed class SecretsController(ISecretRepository secretRepository, IProjectAccessChecker accessChecker) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SecretResponse>>> ListAsync(Guid projectId, CancellationToken ct)
    {
        if (!await accessChecker.CanViewAsync(User, projectId, ct))
            return Forbid();

        var secrets = await secretRepository.GetByProjectAsync(projectId, ct);
        return Ok(secrets.Select(ToResponse).ToList());
    }

    private static SecretResponse ToResponse(Secret s) =>
        new(s.Id, s.ProjectId, s.Handle, s.Name, s.CreatedAtUtc, s.RevokedAtUtc);
}
