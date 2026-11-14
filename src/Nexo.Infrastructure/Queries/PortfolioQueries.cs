using Microsoft.EntityFrameworkCore;
using Nexo.Application.Queries;
using Nexo.Application.Queries.Portfolios;
using Nexo.Domain.Portfolios;
using Nexo.Infrastructure.Persistence;

namespace Nexo.Infrastructure.Queries;

internal sealed class PortfolioQueries(NexoDbContext db) : IPortfolioQueries
{
    public async Task<PortfolioView?> GetAsync(long portfolioId, CancellationToken cancellationToken)
    {
        var portfolio = await db.Portfolios.AsNoTracking()
            .Include(portfolio => portfolio.Fund)
            .SingleOrDefaultAsync(portfolio => portfolio.Id == portfolioId, cancellationToken);

        return portfolio is null ? null : await ToViewAsync(portfolio, cancellationToken);
    }

    public async Task<PortfolioView?> GetForFundAsync(long fundId, PortfolioRequest request, CancellationToken cancellationToken)
    {
        var portfolios = db.Portfolios.AsNoTracking()
            .Include(portfolio => portfolio.Fund)
            .Where(portfolio => portfolio.FundId == fundId && portfolio.IsCurrent);

        if (request.ClassId is not null)
            portfolios = portfolios.Where(portfolio => portfolio.FundClassId == request.ClassId);

        if (request.ReferenceDate is not null)
            portfolios = portfolios.Where(portfolio => portfolio.ReferenceDate == request.ReferenceDate);

        // Em fundos com várias classes, mostramos a de maior PL quando ninguém escolheu uma.
        var portfolio = await portfolios
            .OrderByDescending(portfolio => portfolio.ReferenceDate)
            .ThenByDescending(portfolio => portfolio.NetWorth ?? 0)
            .FirstOrDefaultAsync(cancellationToken);

        return portfolio is null ? null : await ToViewAsync(portfolio, cancellationToken);
    }

    public async Task<IReadOnlyList<PortfolioHistoryPoint>> GetHistoryAsync(long fundId, long? classId, CancellationToken cancellationToken)
    {
        return await db.Portfolios.AsNoTracking()
            .Where(portfolio => portfolio.FundId == fundId && portfolio.IsCurrent)
            .Where(portfolio => classId == null || portfolio.FundClassId == classId)
            .OrderBy(portfolio => portfolio.ReferenceDate)
            .Select(portfolio => new PortfolioHistoryPoint(
                portfolio.ReferenceDate,
                portfolio.Id,
                portfolio.FundClassId,
                portfolio.Revision,
                portfolio.NetWorth,
                db.Positions.Count(position => position.PortfolioId == portfolio.Id),
                db.Positions.Where(position => position.PortfolioId == portfolio.Id && position.IsConfidential).Sum(position => position.MarketValue)))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<PositionView>?> GetPositionsAsync(long portfolioId, PositionSearch search, CancellationToken cancellationToken)
    {
        if (!await db.Portfolios.AnyAsync(portfolio => portfolio.Id == portfolioId, cancellationToken))
            return null;

        var positions = Filter(db.Positions.AsNoTracking().Where(position => position.PortfolioId == portfolioId), search);
        var total = await positions.CountAsync(cancellationToken);

        var items = await Sort(positions, search.Sort)
            .Skip(search.Skip)
            .Take(search.PageSize)
            .Select(position => new PositionView(
                position.Id,
                (int?)position.Block,
                position.ApplicationType,
                position.AssetType,
                position.AssetId,
                position.Asset != null ? position.Asset.Description : null,
                position.Asset != null ? position.Asset.Ticker : null,
                position.Asset != null ? position.Asset.Isin : null,
                position.Asset != null ? position.Asset.IssuerName : null,
                position.TradingIntent,
                position.Quantity,
                position.MarketValue,
                position.ShareOfNetWorth,
                position.IsConfidential,
                position.ConfidentialUntil))
            .ToListAsync(cancellationToken);

        return new PagedResult<PositionView>(items, search.Page, search.PageSize, total);
    }

    private async Task<PortfolioView> ToViewAsync(Portfolio portfolio, CancellationToken cancellationToken)
    {
        var allocation = await db.Positions
            .Where(position => position.PortfolioId == portfolio.Id)
            .GroupBy(position => new { position.ApplicationType, position.IsConfidential })
            .Select(group => new AllocationSlice(
                group.Key.ApplicationType,
                group.Key.IsConfidential,
                group.Count(),
                group.Sum(position => position.MarketValue),
                group.Sum(position => position.ShareOfNetWorth)))
            .ToListAsync(cancellationToken);

        var processedAt = await db.DatasetFiles
            .Where(file => file.Id == portfolio.DatasetFileId)
            .Select(file => file.ProcessedAt)
            .SingleOrDefaultAsync(cancellationToken);

        var confidential = allocation.Where(slice => slice.IsConfidential).ToList();

        return new PortfolioView(
            portfolio.Id,
            portfolio.FundId,
            portfolio.Fund.Name,
            portfolio.FundClassId,
            portfolio.ReferenceDate,
            portfolio.Revision,
            portfolio.IsCurrent,
            portfolio.NetWorth,
            processedAt,
            portfolio.DatasetFileId,
            allocation.Sum(slice => slice.Positions),
            confidential.Count == 0 ? null : confidential.Sum(slice => slice.MarketValue ?? 0),
            allocation.OrderByDescending(slice => slice.MarketValue ?? 0).ToList());
    }

    private static IQueryable<Position> Filter(IQueryable<Position> positions, PositionSearch search)
    {
        if (search.Block is not null)
            positions = positions.Where(position => position.Block == (CdaBlock)search.Block.Value);

        if (!string.IsNullOrWhiteSpace(search.ApplicationType))
            positions = positions.Where(position => position.ApplicationType == search.ApplicationType);

        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var code = search.Search.Trim().ToUpperInvariant();
            var pattern = LikePattern.Contains(search.Search);

            positions = positions.Where(position => position.Asset != null
                && (position.Asset.Ticker == code
                    || position.Asset.Isin == code
                    || EF.Functions.ILike(position.Asset.Description!, pattern)));
        }

        return positions;
    }

    // Valores vazios ficam sempre no fim da lista, seja qual for a ordem escolhida.
    private static IQueryable<Position> Sort(IQueryable<Position> positions, string? sort) => sort switch
    {
        "marketValue" => positions.OrderBy(position => position.MarketValue == null).ThenBy(position => position.MarketValue).ThenBy(position => position.Id),
        "share" => positions.OrderBy(position => position.ShareOfNetWorth == null).ThenBy(position => position.ShareOfNetWorth).ThenBy(position => position.Id),
        "-share" => positions.OrderBy(position => position.ShareOfNetWorth == null).ThenByDescending(position => position.ShareOfNetWorth).ThenBy(position => position.Id),
        "applicationType" => positions.OrderBy(position => position.ApplicationType).ThenByDescending(position => position.MarketValue).ThenBy(position => position.Id),
        _ => positions.OrderBy(position => position.MarketValue == null).ThenByDescending(position => position.MarketValue).ThenBy(position => position.Id),
    };
}
