namespace Nexo.Infrastructure.Caching;

public sealed class CacheOptions
{
    public const string Section = "Cache";

    public bool Enabled { get; set; } = true;

    public TimeSpan TimeToLive { get; set; } = TimeSpan.FromHours(1);

    // A API relê a época no Redis no máximo a cada poucos segundos, para não ir lá a cada requisição.
    public TimeSpan EpochRefreshInterval { get; set; } = TimeSpan.FromSeconds(5);
}
