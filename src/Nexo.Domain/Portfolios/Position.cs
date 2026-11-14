using Nexo.Domain.Assets;

namespace Nexo.Domain.Portfolios;

/// <summary>Uma linha da carteira: quanto o fundo tem de um ativo na data de referência.</summary>
public sealed class Position
{
    private Position()
    {
    }

    public long Id { get; private set; }

    public long PortfolioId { get; private set; }

    public DateOnly ReferenceDate { get; private set; }

    // Posições sigilosas só trazem o valor somado por tipo de aplicação, sem dizer qual é o ativo.
    public long? AssetId { get; private set; }

    public bool IsConfidential { get; private set; }

    public CdaBlock? Block { get; private set; }

    public string ApplicationType { get; private set; } = string.Empty;

    public string? AssetType { get; private set; }

    public string? TradingIntent { get; private set; }

    public bool? IssuerIsRelated { get; private set; }

    public decimal? Quantity { get; private set; }

    public decimal? MarketValue { get; private set; }

    public decimal? CostValue { get; private set; }

    public decimal? BoughtQuantity { get; private set; }

    public decimal? BoughtValue { get; private set; }

    public decimal? SoldQuantity { get; private set; }

    public decimal? SoldValue { get; private set; }

    // Calculado sobre o PL. Fica vazio quando o PL é desconhecido, zero ou negativo.
    public decimal? ShareOfNetWorth { get; private set; }

    public DateOnly? ConfidentialUntil { get; private set; }

    public string? Details { get; private set; }

    public Asset? Asset { get; private set; }

    public Portfolio Portfolio { get; private set; } = null!;
}
