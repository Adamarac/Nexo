using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace Nexo.Infrastructure.Caching;

/// <summary>Conexão única com o Redis, aberta na primeira vez que alguém precisa.</summary>
internal sealed class RedisConnection(IConfiguration configuration) : IAsyncDisposable
{
    public const string EpochKey = "nexo:cache-epoch";

    private readonly Lazy<Task<ConnectionMultiplexer>> _connection = new(() => ConnectAsync(configuration));

    public async Task<IDatabase> GetDatabaseAsync() => (await _connection.Value).GetDatabase();

    public async ValueTask DisposeAsync()
    {
        if (_connection.IsValueCreated)
            await (await _connection.Value).DisposeAsync();
    }

    // Sem AbortOnConnectFail o Nexo continua de pé com o Redis fora; só fica sem cache.
    private static Task<ConnectionMultiplexer> ConnectAsync(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";
        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;
        return ConnectionMultiplexer.ConnectAsync(options);
    }
}
