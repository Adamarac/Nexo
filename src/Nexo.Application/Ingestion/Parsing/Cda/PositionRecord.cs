using Nexo.Domain.Portfolios;

namespace Nexo.Application.Ingestion.Parsing.Cda;

/// <summary>Uma posição lida do CSV, pronta para ir para o banco.</summary>
public sealed record PositionRecord(ReportingFund Fund, string ApplicationType, PositionAmounts Amounts, RowOrigin Origin)
{
    public CdaBlock? Block { get; init; }

    public bool IsConfidential { get; init; }

    public string? AssetType { get; init; }

    public string? TradingIntent { get; init; }

    public bool? IssuerIsRelated { get; init; }

    public DateOnly? ConfidentialUntil { get; init; }

    // Posições sigilosas não dizem qual é o ativo, então ficam sem ele.
    public AssetRecord? Asset { get; init; }

    public string? DetailsJson { get; init; }
}
