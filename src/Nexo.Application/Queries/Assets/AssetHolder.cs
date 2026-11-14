namespace Nexo.Application.Queries.Assets;

/// <summary>Um fundo que tem o ativo na carteira, somando todas as posições dele nesse ativo.</summary>
public sealed record AssetHolder(
    long FundId,
    string FundCnpj,
    string FundName,
    long PortfolioId,
    DateOnly ReferenceDate,
    decimal? Quantity,
    decimal? MarketValue,
    decimal? ShareOfNetWorth);
