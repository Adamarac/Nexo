using Nexo.Application.Ingestion.Messages;
using RabbitMQ.Client;

namespace Nexo.Infrastructure.Messaging;

/// <summary>Exchange e filas do Nexo. Declarar de novo algo que já existe não faz mal.</summary>
internal static class RabbitMqTopology
{
    public const string Exchange = "nexo";
    public const string ProcessQueue = "nexo.process";
    public const string RetryQueue = "nexo.process.retry";
    public const string DeadLetterQueue = "nexo.process.dlq";

    public static async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(ProcessQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(ProcessQueue, Exchange, DatasetDownloaded.RoutingKey, cancellationToken: cancellationToken);

        // Mensagens ficam paradas nesta fila pelo tempo de espera e depois voltam sozinhas para a fila de trabalho.
        var retryArguments = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = Exchange,
            ["x-dead-letter-routing-key"] = DatasetDownloaded.RoutingKey,
        };
        await channel.QueueDeclareAsync(RetryQueue, durable: true, exclusive: false, autoDelete: false, retryArguments, cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(DeadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);
    }
}
