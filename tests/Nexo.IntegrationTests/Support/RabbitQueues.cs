using System.Collections.Concurrent;
using RabbitMQ.Client;

namespace Nexo.IntegrationTests.Support;

/// <summary>Atalhos para olhar e limpar as filas do RabbitMQ durante os testes.</summary>
/// <remarks>Reaproveita uma conexão por endereço: abrir uma a cada consulta deixava os testes lentos e instáveis.</remarks>
public static class RabbitQueues
{
    public const string Process = "nexo.process";
    public const string Retry = "nexo.process.retry";
    public const string DeadLetter = "nexo.process.dlq";

    private static readonly ConcurrentDictionary<string, Lazy<Task<IConnection>>> Connections = new();

    public static async Task PurgeAllAsync(string uri)
    {
        await using var channel = await OpenChannelAsync(uri);

        foreach (var queue in new[] { Process, Retry, DeadLetter })
        {
            try
            {
                await channel.QueuePurgeAsync(queue);
            }
            catch (RabbitMQ.Client.Exceptions.OperationInterruptedException)
            {
                // A fila ainda não existe porque nada foi publicado; não há o que limpar.
                return;
            }
        }
    }

    public static async Task<BasicGetResult?> GetAsync(string uri, string queue)
    {
        await using var channel = await OpenChannelAsync(uri);
        return await channel.BasicGetAsync(queue, autoAck: true);
    }

    public static async Task<uint> CountAsync(string uri, string queue)
    {
        await using var channel = await OpenChannelAsync(uri);
        return await channel.MessageCountAsync(queue);
    }

    private static async Task<IChannel> OpenChannelAsync(string uri)
    {
        var connection = await Connections.GetOrAdd(uri, key => new Lazy<Task<IConnection>>(() =>
            new ConnectionFactory { Uri = new Uri(key), RequestedConnectionTimeout = TimeSpan.FromSeconds(60) }.CreateConnectionAsync())).Value;

        return await connection.CreateChannelAsync();
    }
}
