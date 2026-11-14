using Microsoft.Extensions.Logging;
using Nexo.Application.Ingestion.Ports;
using StackExchange.Redis;

namespace Nexo.Infrastructure.Caching;

internal sealed class RedisCacheInvalidator(RedisConnection redis, ILogger<RedisCacheInvalidator> logger) : ICacheInvalidator
{
    public async Task AdvanceEpochAsync(CancellationToken cancellationToken)
    {
        try
        {
            var database = await redis.GetDatabaseAsync();
            await database.StringIncrementAsync(RedisConnection.EpochKey);
        }
        catch (RedisException exception)
        {
            // Os dados já estão gravados; o pior caso é a API mostrar o cache antigo até ele expirar.
            logger.LogWarning(exception, "Não foi possível avisar o cache sobre dados novos");
        }
    }
}
