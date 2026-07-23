namespace Telechron.Sdk.Persistence;

// R-PER7: configurable age/count retention. A row is eligible for pruning
// once it is BOTH older than MaxAge AND the table holds more than
// MaxCount rows newer than it (count-based retention always keeps at least
// MaxCount rows regardless of age, so a quiet system doesn't lose its only
// recent history to a purely time-based sweep).
public sealed record RetentionPolicy(TimeSpan MaxAge, int MaxCount);
