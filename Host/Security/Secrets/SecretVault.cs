using System.Security.Cryptography;
using System.Text.Json;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;
using Telechron.Sdk.Security;
using Telechron.Sdk.Security.Audit;

namespace Telechron.Host.Security.Secrets;

// R-SEC1/R-SEC5/R-SEC8: the only path through which a raw secret value is
// created, resolved, rotated, or revoked. Every access is audited (R-SEC7) by
// handle only — DetailJson never contains a raw value.
public sealed class SecretVault(
    ISecretRepository secretRepository,
    ISecretEncryptionService encryptionService,
    IAuditLog auditLog) : ISecretVault
{
    public async Task<string> StoreAsync(Guid projectId, string name, ReadOnlyMemory<byte> rawValue, CancellationToken ct = default)
    {
        var handle = GenerateHandle();
        var encrypted = encryptionService.Encrypt(rawValue.Span);

        var secret = new Secret
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Handle = handle,
            Name = name,
            EncryptedValue = encrypted.Ciphertext,
            EncryptionKeyId = encrypted.EncryptionKeyId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        await secretRepository.AddAsync(secret, ct);

        await auditLog.AppendAsync(
            AuditEventKind.SecretCreated,
            JsonSerializer.Serialize(new { handle, name }),
            projectId: projectId,
            ct: ct);

        return handle;
    }

    public async Task<byte[]> ResolveAsync(string handle, CancellationToken ct = default)
    {
        var secret = await secretRepository.GetByHandleAsync(handle, ct)
            ?? throw new SecretNotFoundException(handle);

        if (secret.RevokedAtUtc is not null)
        {
            await auditLog.AppendAsync(
                AuditEventKind.SecretAccessed,
                JsonSerializer.Serialize(new { handle, denied = true, reason = "revoked" }),
                projectId: secret.ProjectId,
                ct: ct);
            throw new SecretRevokedException(handle);
        }

        var value = encryptionService.Decrypt(new EncryptedSecretValue(secret.EncryptedValue, secret.EncryptionKeyId));

        await auditLog.AppendAsync(
            AuditEventKind.SecretAccessed,
            JsonSerializer.Serialize(new { handle, denied = false }),
            projectId: secret.ProjectId,
            ct: ct);

        return value;
    }

    public async Task RevokeAsync(string handle, CancellationToken ct = default)
    {
        var secret = await secretRepository.GetByHandleAsync(handle, ct)
            ?? throw new SecretNotFoundException(handle);

        if (secret.RevokedAtUtc is not null)
            return; // already revoked — idempotent

        var revoked = secret with { RevokedAtUtc = DateTimeOffset.UtcNow };
        await secretRepository.UpdateAsync(revoked, ct);

        await auditLog.AppendAsync(
            AuditEventKind.SecretRevoked,
            JsonSerializer.Serialize(new { handle }),
            projectId: secret.ProjectId,
            ct: ct);
    }

    public async Task RotateAsync(string handle, ReadOnlyMemory<byte> newRawValue, CancellationToken ct = default)
    {
        var secret = await secretRepository.GetByHandleAsync(handle, ct)
            ?? throw new SecretNotFoundException(handle);

        if (secret.RevokedAtUtc is not null)
            throw new SecretRevokedException(handle);

        var encrypted = encryptionService.Encrypt(newRawValue.Span);
        var rotated = secret with { EncryptedValue = encrypted.Ciphertext, EncryptionKeyId = encrypted.EncryptionKeyId };
        await secretRepository.UpdateAsync(rotated, ct);

        await auditLog.AppendAsync(
            AuditEventKind.SecretRotated,
            JsonSerializer.Serialize(new { handle }),
            projectId: secret.ProjectId,
            ct: ct);
    }

    private static string GenerateHandle() =>
        $"sec_{Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16))}";
}
