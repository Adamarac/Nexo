namespace Nexo.Application.Queries.Assets;

public sealed record ConcentrationItem(
    long AssetId,
    string? Description,
    string? Ticker,
    string? Isin,
    string? ApplicationType,
    DateOnly? MaturityDate,
    int Funds,
    decimal TotalMarketValue);
