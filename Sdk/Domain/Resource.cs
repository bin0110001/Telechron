namespace Telechron.Sdk.Domain;

// A managed hardware/software resource on a Machine (R-DM8) — GPUs, Ollama,
// ComfyUI, TTS engines, etc. Resources sharing a non-null ExclusiveGroup
// value are mutually exclusive (only one may be in use at a time); null means
// unconstrained.
public sealed record Resource
{
    public required Guid Id { get; init; }
    public required Guid MachineId { get; init; }
    public required string Kind { get; init; }
    public required string Name { get; init; }
    public string? ExclusiveGroup { get; init; }
}
