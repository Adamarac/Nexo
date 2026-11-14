namespace Nexo.Application.Queries.Funds;

public interface IFundQueries
{
    Task<PagedResult<FundSummary>> SearchAsync(FundSearch search, CancellationToken cancellationToken);

    Task<FundDetails?> GetAsync(long fundId, CancellationToken cancellationToken);
}
