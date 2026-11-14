namespace Nexo.Application.Queries.Funds;

// Nome (ou parte dele) ou CNPJ completo, com ou sem pontuação.
public sealed record FundSearch(
    string? Search = null,
    string? Type = null,
    string? Situation = null,
    string? Sort = null,
    int Page = 1,
    int PageSize = PagedSearch.DefaultPageSize) : PagedSearch(Page, PageSize)
{
    public static readonly IReadOnlyList<string> SortOptions = ["name", "-name", "netWorth", "-netWorth"];
}
