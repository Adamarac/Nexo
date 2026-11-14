namespace Nexo.Application.Queries.Portfolios;

/// <summary>Quanto da carteira está em cada tipo de aplicação.</summary>
public sealed record AllocationSlice(
    string ApplicationType,
    bool IsConfidential,
    int Positions,
    decimal? MarketValue,
    decimal? ShareOfNetWorth);
