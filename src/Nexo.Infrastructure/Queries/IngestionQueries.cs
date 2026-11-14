using Microsoft.EntityFrameworkCore;
using Nexo.Application.Queries;
using Nexo.Application.Queries.Ingestion;
using Nexo.Domain.Ingestion;
using Nexo.Infrastructure.Persistence;

namespace Nexo.Infrastructure.Queries;

internal sealed class IngestionQueries(NexoDbContext db) : IIngestionQueries
{
    public async Task<IngestionStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var byStatus = await db.DatasetFiles
            .GroupBy(file => file.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var pendingMessages = await db.OutboxMessages.CountAsync(message => message.SentAt == null, cancellationToken);
        var lastDiscoveredAt = await db.DatasetFiles.MaxAsync(file => (DateTimeOffset?)file.DiscoveredAt, cancellationToken);
        var lastProcessedAt = await db.DatasetFiles.MaxAsync(file => file.ProcessedAt, cancellationToken);

        return new IngestionStatus(
            byStatus.ToDictionary(item => item.Status.ToString(), item => item.Count),
            pendingMessages,
            lastDiscoveredAt,
            lastProcessedAt);
    }

    public async Task<PagedResult<IngestionFileView>> ListFilesAsync(FileSearch search, CancellationToken cancellationToken)
    {
        var files = db.DatasetFiles.AsNoTracking();

        if (search.Status is not null)
        {
            var status = Enum.Parse<DatasetFileStatus>(search.Status);
            files = files.Where(file => file.Status == status);
        }

        var total = await files.CountAsync(cancellationToken);
        var items = await files
            .OrderByDescending(file => file.DiscoveredAt)
            .ThenByDescending(file => file.Id)
            .Skip(search.Skip)
            .Take(search.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<IngestionFileView>(items.Select(ToView).ToList(), search.Page, search.PageSize, total);
    }

    public async Task<IngestionFileView?> GetFileAsync(long fileId, CancellationToken cancellationToken)
    {
        var file = await db.DatasetFiles.AsNoTracking().SingleOrDefaultAsync(file => file.Id == fileId, cancellationToken);
        return file is null ? null : ToView(file);
    }

    public async Task<PagedResult<RejectionView>> ListRejectionsAsync(long fileId, RejectionSearch search, CancellationToken cancellationToken)
    {
        var rejections = db.RejectedRows.AsNoTracking().Where(row => row.DatasetFileId == fileId);

        if (search.Reason is not null)
        {
            var reason = Enum.Parse<RejectionReason>(search.Reason);
            rejections = rejections.Where(row => row.Reason == reason);
        }

        var total = await rejections.CountAsync(cancellationToken);
        var items = await rejections
            .OrderBy(row => row.FileName)
            .ThenBy(row => row.LineNumber)
            .Skip(search.Skip)
            .Take(search.PageSize)
            .ToListAsync(cancellationToken);

        var views = items
            .Select(row => new RejectionView(row.Id, row.FileName, row.LineNumber, row.Reason.ToString(), row.Detail, row.RawLine))
            .ToList();

        return new PagedResult<RejectionView>(views, search.Page, search.PageSize, total);
    }

    internal static IngestionFileView ToView(DatasetFile file) => new(
        file.Id, file.SourceKey, file.Kind.ToString(), file.Status.ToString(), file.Size, file.ContentHash, file.Attempts,
        file.RowsRead, file.RowsAccepted, file.RowsRejected, file.PortfoliosChanged, file.LastError, file.DiscoveredAt, file.ProcessedAt);
}
