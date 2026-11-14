namespace Nexo.Application.Queries.Issuers;

// Busca por parte de qualquer nome do emissor ou pelo CNPJ (com ou sem pontuação).
// MinSpellings = 2 lista só quem aparece escrito de mais de um jeito.
public sealed record IssuerSearch(
    string? Search = null,
    int? MinSpellings = null,
    string? Sort = null,
    int Page = 1,
    int PageSize = PagedSearch.DefaultPageSize) : PagedSearch(Page, PageSize)
{
    public const string BySpellings = "spellings";
    public const string ByFunds = "funds";
    public const string ByName = "name";

    public static readonly IReadOnlyList<string> SortOptions = [BySpellings, ByFunds, ByName];
}
