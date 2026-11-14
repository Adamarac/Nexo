namespace Nexo.Application.Queries.Ingestion;

public interface IIngestionQueries
{
    Task<IngestionStatus> GetStatusAsync(CancellationToken cancellationToken);

    Task<PagedResult<IngestionFileView>> ListFilesAsync(FileSearch search, CancellationToken cancellationToken);

    Task<IngestionFileView?> GetFileAsync(long fileId, CancellationToken cancellationToken);

    Task<PagedResult<RejectionView>> ListRejectionsAsync(long fileId, RejectionSearch search, CancellationToken cancellationToken);
}
