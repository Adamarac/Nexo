using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexo.Application.Observability;
using Nexo.Application.Queries;
using StackExchange.Redis;

namespace Nexo.Infrastructure.Caching;

/// <summary>Cache das consultas no Redis. A chave leva a "época", que muda a cada ingestão concluída.</summary>
/// <remarks>Quando a época muda, as chaves antigas deixam de ser usadas e expiram sozinhas.</remarks>
internal sealed class RedisQueryCache(
    RedisConnection redis, IOptions<CacheOptions> options, TimeProvider clock, ILogger<RedisQueryCache> logger) : IQueryCache
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly Lock _epochLock = new();
    private long _epoch;
    private DateTimeOffset _epochReadAt = DateTimeOffset.MinValue;

    public async Task<T> GetOrCreateAsync<T>(
        string area, object parameters, Func<CancellationToken, Task<T>> create, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
            return await create(cancellationToken);

        var key = await TryBuildKeyAsync(area, parameters);
        var cached = key is null ? default : await TryReadAsync<T>(key);

        if (cached is not null)
        {
            NexoTelemetry.CacheHits.Add(1, new TagList { { "area", area } });
            return cached;
        }

        NexoTelemetry.CacheMisses.Add(1, new TagList { { "area", area } });
        var value = await create(cancellationToken);

        if (key is not null)
            await TryWriteAsync(key, value);

        return value;
    }

    private async Task<string?> TryBuildKeyAsync(string area, object parameters)
    {
        try
        {
            var epoch = await GetEpochAsync();
            var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(parameters, Json))))[..16];
            return $"nexo:{epoch}:{area}:{hash}";
        }
        catch (RedisException exception)
        {
            logger.LogWarning(exception, "Redis indisponível; respondendo sem cache");
            return null;
        }
    }

    private async Task<long> GetEpochAsync()
    {
        lock (_epochLock)
        {
            if (clock.GetUtcNow() - _epochReadAt < options.Value.EpochRefreshInterval)
                return _epoch;
        }

        var database = await redis.GetDatabaseAsync();
        var epoch = (long?)await database.StringGetAsync(RedisConnection.EpochKey) ?? 0;

        lock (_epochLock)
        {
            _epoch = epoch;
            _epochReadAt = clock.GetUtcNow();
        }

        return epoch;
    }

    private async Task<T?> TryReadAsync<T>(string key)
    {
        try
        {
            var database = await redis.GetDatabaseAsync();
            var value = await database.StringGetAsync(key);
            return value.IsNullOrEmpty ? default : JsonSerializer.Deserialize<T>(value.ToString(), Json);
        }
        catch (Exception exception) when (exception is RedisException or JsonException)
        {
            logger.LogWarning(exception, "Falha ao ler o cache da chave {Key}", key);
            return default;
        }
    }

    private async Task TryWriteAsync<T>(string key, T value)
    {
        try
        {
            var database = await redis.GetDatabaseAsync();
            await database.StringSetAsync(key, JsonSerializer.Serialize(value, Json), options.Value.TimeToLive);
        }
        catch (RedisException exception)
        {
            logger.LogWarning(exception, "Falha ao gravar o cache da chave {Key}", key);
        }
    }
}
