using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexo.Application.Ingestion;
using Nexo.Application.Ingestion.Messages;
using Nexo.Domain.Ingestion;
using Nexo.Infrastructure.Persistence;

namespace Nexo.IntegrationTests.Support;

/// <summary>Executa os passos da ingestão na mão, sem RabbitMQ, para testar a lógica do banco isolada.</summary>
public sealed class IngestionDriver(IServiceProvider services)
{
    public async Task<CheckSummary> CheckCvmAsync()
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CheckCvmForUpdates>().RunAsync(CancellationToken.None);
    }

    // Faz o papel do RabbitMQ: lê os avisos pendentes da outbox e entrega cada um ao processamento.
    public async Task<IReadOnlyList<ProcessingOutcome>> ProcessPendingMessagesAsync()
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoDbContext>();
        var pending = await db.OutboxMessages.Where(message => message.SentAt == null).OrderBy(message => message.Id).ToListAsync();
        var outcomes = new List<ProcessingOutcome>();

        foreach (var outboxMessage in pending)
        {
            var message = JsonSerializer.Deserialize<DatasetDownloaded>(outboxMessage.Payload)!;
            outcomes.Add(await ProcessAsync(message));
            outboxMessage.MarkSent(DateTimeOffset.UtcNow);
        }

        await db.SaveChangesAsync();
        return outcomes;
    }

    public async Task<ProcessingOutcome> ProcessAsync(DatasetDownloaded message)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ProcessDatasetFile>().HandleAsync(message, CancellationToken.None);
    }

    public async Task<ReprocessResult> RequestReprocessAsync(long datasetFileId)
    {
        await using var scope = services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<RequestReprocess>().HandleAsync(datasetFileId, CancellationToken.None);
    }

    public async Task<T> QueryAsync<T>(Func<NexoDbContext, Task<T>> query)
    {
        await using var scope = services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<NexoDbContext>());
    }

    public Task<DatasetFile> LatestFileAsync(string sourceKey) =>
        QueryAsync(db => db.DatasetFiles.AsNoTracking().Where(file => file.SourceKey == sourceKey).OrderByDescending(file => file.Id).FirstAsync());
}
