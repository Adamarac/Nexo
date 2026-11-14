namespace Nexo.Application.Queries.Portfolios;

public sealed record PositionView(
    long Id,
    int? Block,
    string ApplicationType,
    string? AssetType,
    long? AssetId,
    string? AssetDescription,
    string? Ticker,
    string? Isin,
    string? IssuerName,
    string? TradingIntent,
    decimal? Quantity,
    decimal? MarketValue,
    decimal? ShareOfNetWorth,
    bool IsConfidential,
    DateOnly? ConfidentialUntil);
