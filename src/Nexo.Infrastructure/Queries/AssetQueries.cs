using Microsoft.EntityFrameworkCore;
using Nexo.Application.Queries;
using Nexo.Application.Queries.Assets;
using Nexo.Domain.Assets;
using Nexo.Domain.Portfolios;
using Nexo.Infrastructure.Persistence;

namespace Nexo.Infrastructure.Queries;

internal sealed class AssetQueries(NexoDbContext db) : IAssetQueries
{
    public async Task<PagedResult<AssetView>> SearchAsync(AssetSearch search, CancellationToken cancellationToken)
    {
        var assets = db.Assets.AsNoTracking();

        if (search.Block is not null)
            assets = assets.Where(asset => asset.Block == (CdaBlock)search.Block.Value);

        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var code = search.Search.Trim().ToUpperInvariant();
            var pattern = LikePattern.Contains(search.Search);
            assets = assets.Where(asset => asset.Ticker == code || asset.Isin == code || EF.Functions.ILike(asset.Description!, pattern));
        }

        var total = await assets.CountAsync(cancellationToken);
        var items = await assets
            .OrderBy(asset => asset.Description)
            .ThenBy(asset => asset.Id)
            .Skip(search.Skip)
            .Take(search.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AssetView>(items.Select(ToView).ToList(), search.Page, search.PageSize, total);
    }

    public async Task<AssetView?> GetAsync(long assetId, CancellationToken cancellationToken)
    {
        var asset = await db.Assets.AsNoTracking().SingleOrDefaultAsync(asset => asset.Id == assetId, cancellationToken);
        return asset is null ? null : ToView(asset);
    }

    public async Task<PagedResult<AssetHolder>> GetHoldersAsync(long assetId, HolderSearch search, CancellationToken cancellationToken)
    {
        var referenceDate = search.ReferenceDate ?? await db.Positions
            .Where(position => position.AssetId == assetId && position.Portfolio.IsCurrent)
            .MaxAsync(position => (DateOnly?)position.ReferenceDate, cancellationToken);

        if (referenceDate is null)
            return new PagedResult<AssetHolder>([], search.Page, search.PageSize, 0);

        // Um fundo pode ter o mesmo ativo em mais de uma linha (para negociação e até o vencimento), então somamos.
        var holders = db.Positions
            .Where(position => position.AssetId == assetId && position.ReferenceDate == referenceDate && position.Portfolio.IsCurrent)
            .GroupBy(position => new { position.PortfolioId, position.Portfolio.FundId, position.Portfolio.Fund.Name, position.Portfolio.Fund.Cnpj })
            .Select(group => new
            {
                group.Key.PortfolioId,
                group.Key.FundId,
                group.Key.Name,
                group.Key.Cnpj,
                Quantity = group.Sum(position => position.Quantity),
                MarketValue = group.Sum(position => position.MarketValue),
                Share = group.Sum(position => position.ShareOfNetWorth),
            });

        var total = await holders.CountAsync(cancellationToken);
        var page = await holders
            .OrderByDescending(holder => holder.MarketValue ?? 0)
            .ThenBy(holder => holder.FundId)
            .Skip(search.Skip)
            .Take(search.PageSize)
            .ToListAsync(cancellationToken);

        var items = page
            .Select(holder => new AssetHolder(
                holder.FundId, holder.Cnpj.Value, holder.Name, holder.PortfolioId, referenceDate.Value,
                holder.Quantity, holder.MarketValue, holder.Share))
            .ToList();

        return new PagedResult<AssetHolder>(items, search.Page, search.PageSize, total);
    }

    // Caixa, valores a pagar e afins (bloco 8) ficam de fora: não são investimentos em um ativo.
    public async Task<ConcentrationReport> GetConcentrationAsync(ConcentrationSearch search, CancellationToken cancellationToken)
    {
        var referenceDate = search.ReferenceDate ?? await db.Portfolios
            .Where(portfolio => portfolio.IsCurrent)
            .MaxAsync(portfolio => (DateOnly?)portfolio.ReferenceDate, cancellationToken);

        if (referenceDate is null)
            return new ConcentrationReport(null, []);

        var top = await db.Positions
            .Where(position => position.ReferenceDate == referenceDate
                && position.AssetId != null
                && !position.IsConfidential
                && position.Block != CdaBlock.Other
                && position.Portfolio.IsCurrent)
            .GroupBy(position => position.AssetId!.Value)
            .Select(group => new
            {
                AssetId = group.Key,
                Funds = group.Select(position => position.PortfolioId).Distinct().Count(),
                Total = group.Sum(position => position.MarketValue) ?? 0,
            })
            .OrderByDescending(item => item.Total)
            .Take(search.Top)
            .ToListAsync(cancellationToken);

        var assetIds = top.Select(item => item.AssetId).ToList();
        var assets = await db.Assets.AsNoTracking().Where(asset => assetIds.Contains(asset.Id)).ToDictionaryAsync(asset => asset.Id, cancellationToken);

        var items = top
            .Select(item => new ConcentrationItem(
                item.AssetId, assets[item.AssetId].Description, assets[item.AssetId].Ticker, assets[item.AssetId].Isin,
                assets[item.AssetId].ApplicationType, assets[item.AssetId].MaturityDate, item.Funds, item.Total))
            .ToList();

        return new ConcentrationReport(referenceDate, items);
    }

    private static AssetView ToView(Asset asset) => new(
        asset.Id, (int)asset.Block, asset.ApplicationType, asset.AssetType, asset.Isin, asset.Ticker,
        asset.Description, asset.IssuerName, asset.CountryCode, asset.MaturityDate);
}
