using System.Diagnostics;
using System.Text.Json;
using Nexo.Application.Ingestion.Messages;
using Nexo.Infrastructure.Persistence;

namespace Nexo.Infrastructure.Messaging.Outbox;

/// <summary>Coloca avisos na tabela de saída. Eles só são gravados junto com o resto, no SaveChanges.</summary>
internal sealed class OutboxWriter(NexoDbContext db, TimeProvider clock)
{
    public const string DatasetDownloadedType = "nexo.dataset-downloaded.v1";

    public void Enqueue(DatasetDownloaded message) =>
        db.OutboxMessages.Add(OutboxMessage.Create(
            message.MessageId,
            DatasetDownloadedType,
            DatasetDownloaded.RoutingKey,
            JsonSerializer.Serialize(message),
            message.CorrelationId,
            Activity.Current?.Id,
            clock.GetUtcNow()));
}
