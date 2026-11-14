using Nexo.Domain.Portfolios;

namespace Nexo.Domain.Assets;

/// <summary>Ativo que aparece em pelo menos uma carteira.</summary>
public sealed class Asset
{
    private Asset()
    {
    }

    public long Id { get; private set; }

    public string IdentityKey { get; private set; } = string.Empty;

    public CdaBlock Block { get; private set; }

    public string? ApplicationType { get; private set; }

    public string? AssetType { get; private set; }

    public string? Isin { get; private set; }

    public string? Ticker { get; private set; }

    public string? Description { get; private set; }

    public string? IssuerDocument { get; private set; }

    public string? IssuerName { get; private set; }

    public string? CountryCode { get; private set; }

    public DateOnly? MaturityDate { get; private set; }

    // Preenchido quando o ativo é cota de outro fundo, guardando só o CNPJ.
    public string? InvestedFundDocument { get; private set; }
}
