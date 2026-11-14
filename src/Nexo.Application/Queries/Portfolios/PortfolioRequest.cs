namespace Nexo.Application.Queries.Portfolios;

/// <summary>Qual carteira de um fundo mostrar. Sem data, vale a mais recente.</summary>
// Só importa para fundos com mais de uma classe; sem ela, vale a classe com maior PL.
public sealed record PortfolioRequest(DateOnly? ReferenceDate = null, long? ClassId = null);
