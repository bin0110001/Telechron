namespace Telechron.Sdk.Containers;

// R-SYS10: bounds on the warm pool -- "bounded, invalidate-on-change TTL."
public sealed class WarmContainerPoolOptions
{
    public int MaxIdleContainersPerKey { get; set; } = 2;
    public TimeSpan IdleTtl { get; set; } = TimeSpan.FromMinutes(10);
}
