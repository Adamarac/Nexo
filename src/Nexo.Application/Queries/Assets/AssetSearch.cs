namespace Nexo.Application.Queries.Assets;

// Código de negociação, ISIN ou parte da descrição.
public sealed record AssetSearch(
    string? Search = null,
    int? Block = null,
    int Page = 1,
    int PageSize = PagedSearch.DefaultPageSize) : PagedSearch(Page, PageSize);
