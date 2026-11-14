using Nexo.Domain.Common;

namespace Nexo.Domain.Funds;

/// <summary>Classe de um fundo (regra CVM 175). Tem CNPJ próprio e é quem envia a carteira.</summary>
public sealed class FundClass
{
    private FundClass()
    {
    }

    public long Id { get; private set; }

    public long FundId { get; private set; }

    public Cnpj Cnpj { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? ClassType { get; private set; }

    public string? Situation { get; private set; }

    public string? AnbimaClassification { get; private set; }

    public string? TargetAudience { get; private set; }

    public Fund Fund { get; private set; } = null!;
}
