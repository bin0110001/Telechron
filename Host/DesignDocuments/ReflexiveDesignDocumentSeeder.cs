using Microsoft.Extensions.Logging;
using Telechron.Sdk.Domain;
using Telechron.Sdk.Persistence;

namespace Telechron.Host.DesignDocuments;

// R-DM16a: Telechron applies R-DM16 to itself — the Host Sentinel's
// self-repair loop (R-REL3, Phase 9) consults Telechron's own Design
// Document exactly as any managed Project's repair Persona would. This
// seeder creates that reflexive Project/DesignDocument/Requirement set from
// TechDesign.md on first run, idempotently: safe to call on every Host
// startup, a no-op once seeded.
public sealed class ReflexiveDesignDocumentSeeder(
    IUserRepository userRepository,
    IProjectRepository projectRepository,
    IDesignDocumentRepository designDocumentRepository,
    IRequirementRepository requirementRepository,
    IRequirementRevisionRepository requirementRevisionRepository,
    ILogger<ReflexiveDesignDocumentSeeder> logger)
{
    // Well-known marker identifying "Telechron itself" as a self-managed
    // Project — R-DM16a's reflexive scope, not a Project a human created.
    public const string SystemProjectName = "Telechron";
    private const string SystemUserEmail = "system@telechron.internal";

    public async Task SeedFromMarkdownAsync(string techDesignMarkdown, string rootPath, CancellationToken ct = default)
    {
        var projectId = await EnsureSystemProjectAsync(rootPath, ct);
        var designDocumentId = await EnsureDesignDocumentAsync(projectId, ct);

        var parsed = MarkdownRequirementParser.Parse(techDesignMarkdown);
        var existing = (await requirementRepository.GetByDesignDocumentAsync(designDocumentId, ct))
            .ToDictionary(r => r.RequirementId);

        var systemUserId = await EnsureSystemUserAsync(ct);
        var added = 0;
        var updated = 0;

        foreach (var requirement in parsed)
        {
            if (existing.TryGetValue(requirement.RequirementId, out var current))
            {
                if (current.Title == requirement.Title && current.Body == requirement.Body)
                    continue; // unchanged — no new revision needed

                await ApplyRevisionAsync(current, requirement, systemUserId, ct);
                updated++;
            }
            else
            {
                await CreateRequirementAsync(designDocumentId, requirement, systemUserId, ct);
                added++;
            }
        }

        logger.LogInformation(
            "Reflexive Design Document seeded from TechDesign.md: {Added} added, {Updated} updated, {Unchanged} unchanged.",
            added, updated, parsed.Count - added - updated);
    }

    private async Task<Guid> EnsureSystemProjectAsync(string rootPath, CancellationToken ct)
    {
        var existingProjects = await projectRepository.GetAllAsync(ct);
        var systemProject = existingProjects.FirstOrDefault(p => p.Name == SystemProjectName);
        if (systemProject is not null) return systemProject.Id;

        var systemUserId = await EnsureSystemUserAsync(ct);
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = SystemProjectName,
            RootPath = rootPath,
            OwnerUserId = systemUserId,
            // Self-repair of the repair engine is always a privileged path
            // (R-SEC4) regardless of this setting — RequireApproval here is
            // documentation of intent, not the actual enforcement mechanism.
            RepairPolicy = RepairPolicy.RequireApproval,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        await projectRepository.AddAsync(project, ct);
        return project.Id;
    }

    private async Task<Guid> EnsureSystemUserAsync(CancellationToken ct)
    {
        var existing = await userRepository.GetByEmailAsync(SystemUserEmail, ct);
        if (existing is not null) return existing.Id;

        var user = new User
        {
            Id = Guid.NewGuid(),
            DisplayName = "Telechron System",
            Email = SystemUserEmail,
            // Not a human-loginable account — no password is ever set for
            // this identity; login is rejected the same way any hash
            // mismatch is (PasswordHashing.Verify never succeeds against it).
            AuthCredentialHash = string.Empty,
            Role = Role.Admin,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        await userRepository.AddAsync(user, ct);
        return user.Id;
    }

    private async Task<Guid> EnsureDesignDocumentAsync(Guid projectId, CancellationToken ct)
    {
        var existing = await designDocumentRepository.GetByProjectAsync(projectId, ct);
        if (existing is not null) return existing.Id;

        var doc = new DesignDocument
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Title = "Telechron Technical Design",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        await designDocumentRepository.AddAsync(doc, ct);
        return doc.Id;
    }

    private async Task CreateRequirementAsync(Guid designDocumentId, ParsedRequirement parsed, Guid systemUserId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var requirement = new Requirement
        {
            Id = Guid.NewGuid(),
            DesignDocumentId = designDocumentId,
            RequirementId = parsed.RequirementId,
            Title = parsed.Title,
            Body = parsed.Body,
            Status = RequirementStatus.Active,
            CurrentRevisionNumber = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        await requirementRepository.AddAsync(requirement, ct);

        await requirementRevisionRepository.AddAsync(new RequirementRevision
        {
            Id = Guid.NewGuid(),
            RequirementId = requirement.Id,
            RevisionNumber = 1,
            Title = parsed.Title,
            Body = parsed.Body,
            Status = RequirementStatus.Active,
            ChangedByUserId = systemUserId,
            ChangeReason = "Initial seed from TechDesign.md (R-DM16a reflexive self-application).",
            CreatedAtUtc = now,
        }, ct);
    }

    private async Task ApplyRevisionAsync(Requirement current, ParsedRequirement parsed, Guid systemUserId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var nextRevision = current.CurrentRevisionNumber + 1;

        // R-DM16b: this path only runs from re-seeding against an updated
        // TechDesign.md — i.e. a human edited the source document and is
        // re-running the seeder, which is the human-authorship case R-DM16b
        // permits directly (edits made "outside the pipeline"), not an
        // autonomous Persona rewriting its own spec.
        await requirementRevisionRepository.AddAsync(new RequirementRevision
        {
            Id = Guid.NewGuid(),
            RequirementId = current.Id,
            RevisionNumber = nextRevision,
            Title = parsed.Title,
            Body = parsed.Body,
            Status = RequirementStatus.Active,
            ChangedByUserId = systemUserId,
            ChangeReason = "Re-seeded from an updated TechDesign.md.",
            CreatedAtUtc = now,
        }, ct);

        var updatedRequirement = current with
        {
            Title = parsed.Title,
            Body = parsed.Body,
            CurrentRevisionNumber = nextRevision,
            UpdatedAtUtc = now,
        };
        await requirementRepository.UpdateAsync(updatedRequirement, ct);
    }
}
