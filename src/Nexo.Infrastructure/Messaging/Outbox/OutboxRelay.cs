using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nexo.Infrastructure.Persistence;

namespace Nexo.Infrastructure.Messaging.Outbox;

/// <summary>Lê os avisos pendentes da tabela de saída e publica no RabbitMQ.</summary>
internal sealed class OutboxRelay(NexoDbContext db, RabbitMqPublisher publisher, TimeProvider clock, ILogger<OutboxRelay> logger)
{
    private const int BatchSize = 50;

    public async Task<int> PublishPendingAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // SKIP LOCKED deixa dois workers rodarem juntos sem publicar a mesma mensagem duas vezes.
        var pending = await db.OutboxMessages
            .FromSql($"SELECT * FROM outbox_messages WHERE sent_at IS NULL ORDER BY id LIMIT {BatchSize} FOR UPDATE SKIP LOCKED")
            .ToListAsync(cancellationToken);

        var sent = 0;

        foreach (var message in pending)
        {
            try
            {
                await publisher.PublishAsync(ToOutgoing(message), cancellationToken);
                message.MarkSent(clock.GetUtcNow());
                sent++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Se o RabbitMQ está fora, não adianta tentar as outras agora; elas ficam para a próxima rodada.
                message.MarkFailed(exception.Message);
                logger.LogWarning(exception, "Não foi possível publicar a mensagem {MessageId}; nova tentativa em instantes", message.MessageId);
                break;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return sent;
    }

    private static OutgoingMessage ToOutgoing(OutboxMessage message) =>
        new(RabbitMqTopology.Exchange, message.RoutingKey, Encoding.UTF8.GetBytes(message.Payload))
        {
            MessageId = message.MessageId.ToString(),
            Type = message.Type,
            CorrelationId = message.CorrelationId,
            TraceParent = message.TraceParent,
        };
}
