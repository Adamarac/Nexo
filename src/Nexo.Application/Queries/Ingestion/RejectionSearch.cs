namespace Nexo.Application.Queries.Ingestion;

public sealed record RejectionSearch(
    string? Reason = null,
    int Page = 1,
    int PageSize = PagedSearch.DefaultPageSize) : PagedSearch(Page, PageSize);
