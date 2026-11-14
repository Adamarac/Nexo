using Microsoft.EntityFrameworkCore;
using Nexo.Application.Queries.Dashboard;
using Nexo.Infrastructure.Persistence;

namespace Nexo.Infrastructure.Queries;

internal sealed class DashboardQueries(NexoDbContext db) : IDashboardQueries
{
    private const int RecentFiles = 10;
    private const int FundTypes = 10;
    private const string UnknownType = "Não informado";

    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken cancellationToken)
    {
        var currentPortfolios = db.Portfolios.Where(portfolio => portfolio.IsCurrent);

        var funds = await db.Funds.CountAsync(cancellationToken);
        var fundsWithPortfolio = await currentPortfolios.Select(portfolio => portfolio.FundId).Distinct().CountAsync(cancellationToken);
        var assets = await db.Assets.CountAsync(cancellationToken);
        var portfolios = await currentPortfolios.CountAsync(cancellationToken);
        var latestReferenceDate = await currentPortfolios.MaxAsync(portfolio => (DateOnly?)portfolio.ReferenceDate, cancellationToken);
        var lastProcessedAt = await db.DatasetFiles.MaxAsync(file => file.ProcessedAt, cancellationToken);

        var fundsByType = await db.Funds
            .Where(fund => currentPortfolios.Any(portfolio => portfolio.FundId == fund.Id))
            .GroupBy(fund => fund.FundType ?? UnknownType)
            .Select(group => new { Label = group.Key, Count = group.Count() })
            .OrderByDescending(item => item.Count)
            .Take(FundTypes)
            .ToListAsync(cancellationToken);

        var recentFiles = await db.DatasetFiles.AsNoTracking()
            .OrderByDescending(file => file.DiscoveredAt)
            .Take(RecentFiles)
            .ToListAsync(cancellationToken);

        return new DashboardSummary(
            funds, fundsWithPortfolio, assets, portfolios, latestReferenceDate, lastProcessedAt,
            fundsByType.Select(item => new CountByLabel(item.Label, item.Count)).ToList(),
            recentFiles.Select(IngestionQueries.ToView).ToList());
    }
}
