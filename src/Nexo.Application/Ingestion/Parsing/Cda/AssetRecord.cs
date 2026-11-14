using Nexo.Domain.Assets;
using Nexo.Domain.Portfolios;

namespace Nexo.Application.Ingestion.Parsing.Cda;

/// <summary>O que uma linha conta sobre o ativo. Só a identidade e o bloco são obrigatórios.</summary>
public sealed record AssetRecord(AssetIdentity Identity, CdaBlock Block)
{
    public string? ApplicationType { get; init; }

    public string? AssetType { get; init; }

    public string? Isin { get; init; }

    public string? Ticker { get; init; }

    public string? Description { get; init; }

    public string? IssuerDocument { get; init; }

    public string? IssuerName { get; init; }

    public string? CountryCode { get; init; }

    public DateOnly? MaturityDate { get; init; }

    public string? InvestedFundDocument { get; init; }
}
