namespace Nexo.Application.Queries.Funds;

public sealed record FundClassView(
    long Id,
    string Cnpj,
    string Name,
    string? ClassType,
    string? Situation,
    string? AnbimaClassification,
    string? TargetAudience);
