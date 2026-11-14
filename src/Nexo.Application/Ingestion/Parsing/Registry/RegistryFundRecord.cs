using Nexo.Domain.Common;

namespace Nexo.Application.Ingestion.Parsing.Registry;

/// <summary>Uma linha de registro_fundo.csv.</summary>
public sealed record RegistryFundRecord(long RegistryId, Cnpj Cnpj, string Name, DateOnly? RegisteredAt)
{
    public string? FundType { get; init; }

    public string? Situation { get; init; }

    public string? AdministratorName { get; init; }

    public string? ManagerName { get; init; }

    public decimal? NetWorth { get; init; }

    public DateOnly? NetWorthDate { get; init; }
}
