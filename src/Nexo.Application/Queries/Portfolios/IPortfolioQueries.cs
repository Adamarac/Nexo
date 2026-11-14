namespace Nexo.Application.Queries.Portfolios;

public interface IPortfolioQueries
{
    Task<PortfolioView?> GetAsync(long portfolioId, CancellationToken cancellationToken);

    Task<PortfolioView?> GetForFundAsync(long fundId, PortfolioRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<PortfolioHistoryPoint>> GetHistoryAsync(long fundId, long? classId, CancellationToken cancellationToken);

    Task<PagedResult<PositionView>?> GetPositionsAsync(long portfolioId, PositionSearch search, CancellationToken cancellationToken);
}
