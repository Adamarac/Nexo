using Microsoft.EntityFrameworkCore;
using Nexo.Application.Ingestion.Messages;
using Nexo.Application.Ingestion.Ports;
using Nexo.Domain.Ingestion;
using Nexo.Infrastructure.Messaging.Outbox;

namespace Nexo.Infrastructure.Persistence;

internal sealed class IngestionStore(NexoDbContext db, OutboxWriter outbox) : IIngestionStore
{
    public Task<DatasetFile?> FindAsync(long id, CancellationToken cancellationToken) =>
        db.DatasetFiles.SingleOrDefaultAsync(file => file.Id == id, cancellationToken);

    public Task<DatasetFile?> FindLatestAsync(string sourceKey, CancellationToken cancellationToken) =>
        db.DatasetFiles
            .Where(file => file.SourceKey == sourceKey)
            .OrderByDescending(file => file.DiscoveredAt)
            .ThenByDescending(file => file.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<DatasetFile?> FindByHashAsync(string sourceKey, string contentHash, CancellationToken cancellationToken) =>
        db.DatasetFiles.SingleOrDefaultAsync(file => file.SourceKey == sourceKey && file.ContentHash == contentHash, cancellationToken);

    public async Task AddDownloadedAsync(
        DatasetFile file, Func<DatasetFile, DatasetDownloaded> createMessage, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // O primeiro SaveChanges gera o id do arquivo, que vai dentro do aviso.
        db.DatasetFiles.Add(file);
        await db.SaveChangesAsync(cancellationToken);

        outbox.Enqueue(createMessage(file));
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveWithMessageAsync(DatasetDownloaded message, CancellationToken cancellationToken)
    {
        outbox.Enqueue(message);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task CompleteAsync(DatasetFile file, CancellationToken cancellationToken)
    {
        var olderVersions = await db.DatasetFiles
            .Where(other => other.SourceKey == file.SourceKey
                && other.Id != file.Id
                && other.DiscoveredAt <= file.DiscoveredAt
                && other.Status == DatasetFileStatus.Processed)
            .ToListAsync(cancellationToken);

        foreach (var olderVersion in olderVersions)
            olderVersion.Supersede();

        await db.SaveChangesAsync(cancellationToken);
    }
}
