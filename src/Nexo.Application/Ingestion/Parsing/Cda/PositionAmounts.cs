namespace Nexo.Application.Ingestion.Parsing.Cda;

/// <summary>Quantidades e valores de uma posição. Null quer dizer "não informado", que é diferente de zero.</summary>
public sealed record PositionAmounts(
    decimal? Quantity,
    decimal? MarketValue,
    decimal? CostValue,
    decimal? BoughtQuantity,
    decimal? BoughtValue,
    decimal? SoldQuantity,
    decimal? SoldValue);
