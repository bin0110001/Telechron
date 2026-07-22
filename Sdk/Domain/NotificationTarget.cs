namespace Telechron.Sdk.Domain;

// A channel a User can be notified through (R-DM15). Kind is a free-form
// discriminator (e.g. "email", "discord") since notification channels are
// themselves provided by Connector modules (R-DM11) added in later phases.
public sealed class NotificationTarget
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required string Kind { get; init; }
    public required string Address { get; init; }
}
