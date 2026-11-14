namespace Nexo.Application.Queries.Issuers;

public interface IIssuerQueries
{
    Task<PagedResult<IssuerSummary>> SearchAsync(IssuerSearch search, CancellationToken cancellationToken);

    Task<IssuerDetail?> GetAsync(string document, CancellationToken cancellationToken);

    Task<PagedResult<IssuerFund>> GetFundsAsync(string document, IssuerFundSearch search, CancellationToken cancellationToken);
}
