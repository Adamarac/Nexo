namespace Nexo.Application.Queries.Portfolios;

// Procura na descrição, no código de negociação e no ISIN do ativo.
public sealed record PositionSearch(
    int? Block = null,
    string? ApplicationType = null,
    string? Search = null,
    string? Sort = null,
    int Page = 1,
    int PageSize = PagedSearch.DefaultPageSize) : PagedSearch(Page, PageSize)
{
    public static readonly IReadOnlyList<string> SortOptions =
        ["marketValue", "-marketValue", "share", "-share", "applicationType"];
}
