namespace Nexo.Application.Queries.Portfolios;

public sealed record PortfolioHistoryPoint(
    DateOnly ReferenceDate,
    long PortfolioId,
    long? FundClassId,
    int Revision,
    decimal? NetWorth,
    int PositionCount,
    decimal? ConfidentialValue);
