namespace Nexo.Application.Queries.Assets;

public sealed record AssetView(
    long Id,
    int Block,
    string? ApplicationType,
    string? AssetType,
    string? Isin,
    string? Ticker,
    string? Description,
    string? IssuerName,
    string? CountryCode,
    DateOnly? MaturityDate);
