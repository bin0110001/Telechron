namespace Telechron.Host.Persistence.Entities;

// EF Core-mapped row shape for Resource (R-DM8).
public sealed class ResourceEntity
{
    public Guid Id { get; set; }
    public Guid MachineId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ExclusiveGroup { get; set; }

    public MachineEntity? Machine { get; set; }
}
