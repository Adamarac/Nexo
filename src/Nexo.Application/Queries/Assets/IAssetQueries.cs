namespace Nexo.Application.Queries.Assets;

public interface IAssetQueries
{
    Task<PagedResult<AssetView>> SearchAsync(AssetSearch search, CancellationToken cancellationToken);

    Task<AssetView?> GetAsync(long assetId, CancellationToken cancellationToken);

    Task<PagedResult<AssetHolder>> GetHoldersAsync(long assetId, HolderSearch search, CancellationToken cancellationToken);

    Task<PagedResult<AssetGroup>> SearchGroupsAsync(AssetSearch search, CancellationToken cancellationToken);

    Task<AssetGroupDetail?> GetMaturitiesAsync(long assetId, CancellationToken cancellationToken);

    Task<ConcentrationReport> GetConcentrationAsync(ConcentrationSearch search, CancellationToken cancellationToken);
}
