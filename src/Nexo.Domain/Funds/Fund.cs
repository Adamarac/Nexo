using Nexo.Domain.Common;

namespace Nexo.Domain.Funds;

/// <summary>Fundo de investimento como aparece no cadastro da CVM.</summary>
public sealed class Fund
{
    private readonly List<FundClass> _classes = [];

    private Fund()
    {
    }

    public long Id { get; private set; }

    public Cnpj Cnpj { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? FundType { get; private set; }

    public string? Situation { get; private set; }

    public string? AdministratorName { get; private set; }

    public string? ManagerName { get; private set; }

    public decimal? NetWorth { get; private set; }

    public DateOnly? NetWorthDate { get; private set; }

    // A carteira pode chegar antes do cadastro. Nesse caso criamos o fundo só com CNPJ e nome,
    // e o cadastro completa o resto quando for processado.
    public bool IsPlaceholder { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<FundClass> Classes => _classes;
}
