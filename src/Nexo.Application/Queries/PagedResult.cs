namespace Nexo.Application.Queries;

/// <summary>Uma página de resultados e o total de itens, para a tela montar a paginação.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems);
