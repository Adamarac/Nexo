using Nexo.Domain.Funds;

namespace Nexo.Domain.Portfolios;

/// <summary>Carteira de um fundo numa data de referência.</summary>
/// <remarks>Quando a CVM republica o mês com mudanças, nasce uma nova revisão e a anterior fica guardada.</remarks>
public sealed class Portfolio
{
    private Portfolio()
    {
    }

    public long Id { get; private set; }

    public long FundId { get; private set; }

    public long? FundClassId { get; private set; }

    public DateOnly ReferenceDate { get; private set; }

    public int Revision { get; private set; }

    public bool IsCurrent { get; private set; }

    public decimal? NetWorth { get; private set; }

    public string Fingerprint { get; private set; } = string.Empty;

    public long DatasetFileId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public Fund Fund { get; private set; } = null!;

    public FundClass? FundClass { get; private set; }
}
