namespace Nexo.Application.Queries.Issuers;

// Spelling filtra os fundos que escrevem o nome exatamente desse jeito.
public sealed record IssuerFundSearch(
    string? Spelling = null,
    int Page = 1,
    int PageSize = PagedSearch.DefaultPageSize) : PagedSearch(Page, PageSize);
