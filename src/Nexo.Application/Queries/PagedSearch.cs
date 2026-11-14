namespace Nexo.Application.Queries;

/// <summary>Parâmetros de paginação comuns a todas as listas da API.</summary>
public abstract record PagedSearch(int Page, int PageSize)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public int Skip => (Page - 1) * PageSize;
}
