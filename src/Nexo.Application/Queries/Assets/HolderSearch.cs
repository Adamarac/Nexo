namespace Nexo.Application.Queries.Assets;

// Sem data, vale a mais recente em que o ativo aparece.
public sealed record HolderSearch(
    DateOnly? ReferenceDate = null,
    int Page = 1,
    int PageSize = PagedSearch.DefaultPageSize) : PagedSearch(Page, PageSize);
