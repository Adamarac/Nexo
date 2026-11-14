namespace Nexo.Application.Queries.Ingestion;

public sealed record FileSearch(
    string? Status = null,
    int Page = 1,
    int PageSize = PagedSearch.DefaultPageSize) : PagedSearch(Page, PageSize);
