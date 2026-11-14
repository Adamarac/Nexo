namespace Nexo.Application.Queries.Assets;

// Maturities é quantos vencimentos a linha junta; sem agrupar, é sempre 1.
public sealed record ConcentrationItem(
    long AssetId,
    string? Description,
    string? Ticker,
    string? Isin,
    string? ApplicationType,
    DateOnly? MaturityDate,
    int Funds,
    decimal TotalMarketValue,
    int Maturities = 1);
