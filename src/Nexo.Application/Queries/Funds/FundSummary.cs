namespace Nexo.Application.Queries.Funds;

public sealed record FundSummary(
    long Id,
    string Cnpj,
    string Name,
    string? FundType,
    string? Situation,
    decimal? NetWorth,
    DateOnly? LatestReferenceDate);
