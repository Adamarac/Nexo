namespace Nexo.Domain.Assets;

/// <summary>Como um fundo escreveu o nome de um emissor (identificado pelo CNPJ) na carteira de um mês.</summary>
/// <remarks>As linhas são gravadas pela carga de carteiras; a aplicação só lê.</remarks>
public sealed class IssuerSpelling
{
    private IssuerSpelling()
    {
    }

    public long Id { get; private set; }

    public string IssuerDocument { get; private set; } = string.Empty;

    public string IssuerName { get; private set; } = string.Empty;

    public long FundId { get; private set; }

    public long? FundClassId { get; private set; }

    public DateOnly ReferenceDate { get; private set; }
}
