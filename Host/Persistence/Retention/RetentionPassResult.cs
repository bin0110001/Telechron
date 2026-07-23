namespace Telechron.Host.Persistence.Retention;

public sealed record RetentionPassResult(string EntityTypeName, int ArchivedCount, int SkippedRepairLineageCount);
