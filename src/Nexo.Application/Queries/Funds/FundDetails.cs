namespace Nexo.Application.Queries.Funds;

public sealed record FundDetails(
    long Id,
    string Cnpj,
    string Name,
    string? FundType,
    string? Situation,
    string? AdministratorName,
    string? ManagerName,
    decimal? NetWorth,
    DateOnly? NetWorthDate,
    bool IsPlaceholder,
    IReadOnlyList<FundClassView> Classes,
    IReadOnlyList<DateOnly> ReferenceDates);
