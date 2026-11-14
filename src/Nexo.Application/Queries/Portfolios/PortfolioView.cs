namespace Nexo.Application.Queries.Portfolios;

/// <summary>Carteira de um fundo numa data, com a distribuição por tipo de aplicação.</summary>
public sealed record PortfolioView(
    long PortfolioId,
    long FundId,
    string FundName,
    long? FundClassId,
    DateOnly ReferenceDate,
    int Revision,
    bool IsCurrent,
    decimal? NetWorth,
    DateTimeOffset? ProcessedAt,
    long DatasetFileId,
    int PositionCount,
    decimal? ConfidentialValue,
    IReadOnlyList<AllocationSlice> Allocation);
