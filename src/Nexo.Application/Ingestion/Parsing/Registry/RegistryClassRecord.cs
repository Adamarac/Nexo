using Nexo.Domain.Common;

namespace Nexo.Application.Ingestion.Parsing.Registry;

/// <summary>Uma linha de registro_classe.csv. Aponta para o fundo pelo ID de registro.</summary>
public sealed record RegistryClassRecord(long FundRegistryId, Cnpj Cnpj, string Name, DateOnly? RegisteredAt)
{
    public string? ClassType { get; init; }

    public string? Situation { get; init; }

    public string? AnbimaClassification { get; init; }

    public string? TargetAudience { get; init; }
}
